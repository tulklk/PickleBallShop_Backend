using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PickleBall.Application.DTOs.Notifications;
using PickleBall.Application.DTOs.Orders;
using PickleBall.Application.DTOs.Payments;
using PickleBall.Application.Interfaces;
using PickleBall.Domain.Entities;
using PickleBall.Infrastructure.Data;

namespace PickleBall.Infrastructure.Services;

public class OrderService : IOrderService
{
    private readonly ApplicationDbContext _context;
    private readonly IMapper _mapper;
    private readonly IPaymentService _paymentService;
    private readonly IEmailService _emailService;
    private readonly ILogger<OrderService> _logger;
    private readonly IVoucherService _voucherService;
    private readonly IAdminOrderNotificationPublisher _adminOrderNotificationPublisher;

    public OrderService(
        ApplicationDbContext context,
        IMapper mapper,
        IPaymentService paymentService,
        IEmailService emailService,
        ILogger<OrderService> logger,
        IVoucherService voucherService,
        IAdminOrderNotificationPublisher adminOrderNotificationPublisher)
    {
        _context = context;
        _mapper = mapper;
        _paymentService = paymentService;
        _emailService = emailService;
        _logger = logger;
        _voucherService = voucherService;
        _adminOrderNotificationPublisher = adminOrderNotificationPublisher;
    }

    public async Task<OrderDto> CreateOrderAsync(Guid userId, CreateOrderRequest request)
    {
        // Get user cart
        var cart = await _context.Carts
            .Include(c => c.Items)
                .ThenInclude(ci => ci.Product)
            .Include(c => c.Items)
                .ThenInclude(ci => ci.ProductVariant)
            .FirstOrDefaultAsync(c => c.UserId == userId);

        if (cart == null || !cart.Items.Any())
        {
            throw new InvalidOperationException("Cart is empty");
        }

        // Validate stock (check at variant level if product has variants)
        foreach (var item in cart.Items)
        {
            if (item.Product.HasVariants && item.ProductVariant != null)
            {
                // Check stock at variant level
                if (item.ProductVariant.StockQuantity < item.Quantity)
                {
                    throw new InvalidOperationException($"Insufficient stock for product {item.Product.Name} variant. Available: {item.ProductVariant.StockQuantity}");
                }
            }
            else
            {
                // Check stock at product level
                if (item.Product.StockQuantity < item.Quantity)
                {
                    throw new InvalidOperationException($"Insufficient stock for product {item.Product.Name}. Available: {item.Product.StockQuantity}");
                }
            }
        }

        // Generate order number
        var orderNumber = $"ORD-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpper()}";

        // Calculate subtotal
        var subtotal = cart.Items.Sum(ci => ci.UnitPriceSnapshot * ci.Quantity);

        decimal discountAmount = 0;
        Guid? voucherId = null;

        if (!string.IsNullOrWhiteSpace(request.VoucherCode))
        {
            var validateResult = await _voucherService.ValidateAndCalculateAsync(
                request.VoucherCode,
                subtotal,
                userId,
                null);

            if (!validateResult.isValid)
            {
                throw new InvalidOperationException(validateResult.message);
            }

            discountAmount = validateResult.discountAmount;
            voucherId = validateResult.voucher?.Id;
        }

        var totalAmount = subtotal - discountAmount;

        // Create order
        var order = new Order
        {
            OrderNumber = orderNumber,
            UserId = userId,
            TotalAmount = totalAmount,
            PaymentStatus = PaymentStatus.Pending,
            // Mặc định đơn hàng ở trạng thái "Đang xử lý"
            OrderStatus = OrderStatus.Processing,
            PaymentMethod = request.PaymentMethod,
            ShippingFullName = request.ShippingAddress.FullName,
            ShippingPhoneNumber = request.ShippingAddress.PhoneNumber,
            ShippingAddressLine = request.ShippingAddress.AddressLine,
            ShippingWard = request.ShippingAddress.Ward,
            ShippingDistrict = request.ShippingAddress.District,
            ShippingCity = request.ShippingAddress.City,
            VoucherId = voucherId,
            VoucherDiscountAmount = discountAmount > 0 ? discountAmount : null
        };

        // Create order items
        foreach (var cartItem in cart.Items)
        {
            // Build variant info snapshot for display
            string? variantInfoSnapshot = null;
            if (cartItem.ProductVariant != null)
            {
                var variantParts = new List<string>();
                if (!string.IsNullOrWhiteSpace(cartItem.ProductVariant.Color))
                    variantParts.Add($"Màu: {cartItem.ProductVariant.Color}");
                if (!string.IsNullOrWhiteSpace(cartItem.ProductVariant.Size))
                    variantParts.Add($"Size: {cartItem.ProductVariant.Size}");
                if (!string.IsNullOrWhiteSpace(cartItem.ProductVariant.Spec))
                    variantParts.Add($"Thông số: {cartItem.ProductVariant.Spec}");
                
                if (variantParts.Any())
                {
                    variantInfoSnapshot = string.Join(", ", variantParts);
                }
            }

            var orderItem = new OrderItem
            {
                OrderId = order.Id,
                ProductId = cartItem.ProductId,
                ProductVariantId = cartItem.ProductVariantId,
                ProductNameSnapshot = cartItem.Product.Name,
                VariantInfoSnapshot = variantInfoSnapshot,
                UnitPrice = cartItem.UnitPriceSnapshot,
                Quantity = cartItem.Quantity,
                LineTotal = cartItem.UnitPriceSnapshot * cartItem.Quantity
            };
            order.Items.Add(orderItem);

            // Update inventory (at variant level if product has variants)
            if (cartItem.Product.HasVariants && cartItem.ProductVariant != null)
            {
                // Update variant inventory
                cartItem.ProductVariant.StockQuantity -= cartItem.Quantity;
            }
            else
            {
                // Update product inventory
                cartItem.Product.StockQuantity -= cartItem.Quantity;
            }

            // Create inventory transaction
            var transaction = new InventoryTransaction
            {
                ProductId = cartItem.ProductId,
                ChangeQuantity = -cartItem.Quantity,
                Reason = $"Order {orderNumber}"
            };
            _context.InventoryTransactions.Add(transaction);
        }

        _context.Orders.Add(order);

        // Track voucher usage
        if (voucherId.HasValue && discountAmount > 0)
        {
            var usage = new VoucherUsage
            {
                VoucherId = voucherId.Value,
                OrderId = order.Id,
                UserId = userId
            };
            _context.VoucherUsages.Add(usage);

            var voucher = await _context.Vouchers.FirstOrDefaultAsync(v => v.Id == voucherId.Value);
            if (voucher != null)
            {
                voucher.UsedCount += 1;
            }
        }

        // Clear cart
        _context.CartItems.RemoveRange(cart.Items);

        await _context.SaveChangesAsync();

        try
        {
            var notificationDto = await BuildNewOrderNotificationDtoAsync(order, userId, isGuest: false, CancellationToken.None);
            await _adminOrderNotificationPublisher.NotifyNewOrderAsync(notificationDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send admin new-order notification for order {OrderId}", order.Id);
        }

        // For COD orders, send confirmation email immediately since order is confirmed
        if (request.PaymentMethod == PaymentMethod.COD)
        {
            try
            {
                // Detach the tracked order entity first to avoid tracking conflicts
                _context.Entry(order).State = Microsoft.EntityFrameworkCore.EntityState.Detached;
                
                // Reload order with all navigation properties needed for email using AsNoTracking
                // This ensures we get fresh data from database, similar to test endpoint
                var orderWithDetails = await _context.Orders
                    .AsNoTracking()
                    .Include(o => o.User)
                    .Include(o => o.Items)
                        .ThenInclude(oi => oi.Product)
                            .ThenInclude(p => p.Images)
                    .Include(o => o.Items)
                        .ThenInclude(oi => oi.ProductVariant)
                    .FirstOrDefaultAsync(o => o.Id == order.Id);

                if (orderWithDetails != null)
                {
                    // Validate user email before sending
                    if (orderWithDetails.User != null && !string.IsNullOrWhiteSpace(orderWithDetails.User.Email))
                    {
                        _logger.LogInformation("Sending order confirmation email for COD order {OrderId} to {Email}", 
                            order.Id, orderWithDetails.User.Email);
                        
                        await _emailService.SendOrderConfirmationEmailAsync(orderWithDetails);
                        
                        _logger.LogInformation("Order confirmation email sent successfully for COD order {OrderId} to {Email}", 
                            order.Id, orderWithDetails.User.Email);
                    }
                    else
                    {
                        _logger.LogWarning("Cannot send order confirmation email for COD order {OrderId}: User email not available", order.Id);
                    }
                }
                else
                {
                    _logger.LogWarning("Cannot reload order {OrderId} for email sending", order.Id);
                }
            }
            catch (Exception ex)
            {
                // Log error but don't fail order creation
                _logger.LogError(ex, "Failed to send order confirmation email for COD order {OrderId}. Error: {Error}", 
                    order.Id, ex.Message);
            }
        }

        return _mapper.Map<OrderDto>(order);
    }

    public async Task<OrderDto> CreateGuestOrderAsync(CreateGuestOrderRequest request)
    {
        if (request.Items == null || !request.Items.Any())
        {
            throw new InvalidOperationException("Giỏ hàng trống");
        }

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            throw new InvalidOperationException("Email là bắt buộc");
        }

        // Load products and variants for the cart items
        var productIds = request.Items.Select(i => i.ProductId).ToList();
        var variantIds = request.Items.Where(i => i.ProductVariantId.HasValue).Select(i => i.ProductVariantId!.Value).ToList();

        var products = await _context.Products
            .Include(p => p.Variants)
            .Where(p => productIds.Contains(p.Id))
            .ToListAsync();

        var variants = variantIds.Any()
            ? await _context.ProductVariants.Where(v => variantIds.Contains(v.Id)).ToListAsync()
            : new List<ProductVariant>();

        // Validate products exist and have enough stock
        foreach (var item in request.Items)
        {
            var product = products.FirstOrDefault(p => p.Id == item.ProductId);
            if (product == null)
            {
                throw new InvalidOperationException($"Không tìm thấy sản phẩm với ID {item.ProductId}");
            }

            if (product.HasVariants && item.ProductVariantId.HasValue)
            {
                var variant = variants.FirstOrDefault(v => v.Id == item.ProductVariantId);
                if (variant == null)
                {
                    throw new InvalidOperationException($"Không tìm thấy biến thể sản phẩm với ID {item.ProductVariantId}");
                }
                if (variant.StockQuantity < item.Quantity)
                {
                    throw new InvalidOperationException($"Sản phẩm {product.Name} biến thể không đủ số lượng. Còn lại: {variant.StockQuantity}");
                }
            }
            else
            {
                if (product.StockQuantity < item.Quantity)
                {
                    throw new InvalidOperationException($"Sản phẩm {product.Name} không đủ số lượng. Còn lại: {product.StockQuantity}");
                }
            }
        }

        // Generate order number
        var orderNumber = $"ORD-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpper()}";

        // Calculate subtotal
        decimal subtotal = 0;
        foreach (var item in request.Items)
        {
            var product = products.First(p => p.Id == item.ProductId);
            decimal price;
            
            if (product.HasVariants && item.ProductVariantId.HasValue)
            {
                var variant = variants.First(v => v.Id == item.ProductVariantId);
                price = variant.Price;
            }
            else
            {
                price = product.Price; // Price is the current selling price
            }
            
            subtotal += price * item.Quantity;
        }

        decimal discountAmount = 0;
        Guid? voucherId = null;

        if (!string.IsNullOrWhiteSpace(request.VoucherCode))
        {
            var validateResult = await _voucherService.ValidateAndCalculateAsync(
                request.VoucherCode,
                subtotal,
                null,
                request.Email);

            if (!validateResult.isValid)
            {
                throw new InvalidOperationException(validateResult.message);
            }

            discountAmount = validateResult.discountAmount;
            voucherId = validateResult.voucher?.Id;
        }

        var totalAmount = subtotal - discountAmount;

        // Create order
        var order = new Order
        {
            OrderNumber = orderNumber,
            UserId = null, // Guest order
            GuestEmail = request.Email,
            GuestFullName = request.FullName,
            TotalAmount = totalAmount,
            PaymentStatus = PaymentStatus.Pending,
            // Mặc định đơn hàng ở trạng thái "Đang xử lý"
            OrderStatus = OrderStatus.Processing,
            PaymentMethod = request.PaymentMethod,
            ShippingFullName = request.ShippingAddress.FullName,
            ShippingPhoneNumber = request.ShippingAddress.PhoneNumber,
            ShippingAddressLine = request.ShippingAddress.AddressLine,
            ShippingWard = request.ShippingAddress.Ward,
            ShippingDistrict = request.ShippingAddress.District,
            ShippingCity = request.ShippingAddress.City,
            VoucherId = voucherId,
            VoucherDiscountAmount = discountAmount > 0 ? discountAmount : null
        };

        // Create order items and update inventory
        foreach (var cartItem in request.Items)
        {
            var product = products.First(p => p.Id == cartItem.ProductId);
            ProductVariant? variant = null;
            decimal unitPrice;
            string? variantInfoSnapshot = null;

            if (product.HasVariants && cartItem.ProductVariantId.HasValue)
            {
                variant = variants.First(v => v.Id == cartItem.ProductVariantId);
                unitPrice = variant.Price;

                var variantParts = new List<string>();
                if (!string.IsNullOrWhiteSpace(variant.Color))
                    variantParts.Add($"Màu: {variant.Color}");
                if (!string.IsNullOrWhiteSpace(variant.Size))
                    variantParts.Add($"Size: {variant.Size}");
                if (!string.IsNullOrWhiteSpace(variant.Spec))
                    variantParts.Add($"Thông số: {variant.Spec}");
                
                if (variantParts.Any())
                {
                    variantInfoSnapshot = string.Join(", ", variantParts);
                }

                // Update variant inventory
                variant.StockQuantity -= cartItem.Quantity;
            }
            else
            {
                unitPrice = product.Price; // Price is the current selling price
                // Update product inventory
                product.StockQuantity -= cartItem.Quantity;
            }

            var orderItem = new OrderItem
            {
                OrderId = order.Id,
                ProductId = cartItem.ProductId,
                ProductVariantId = cartItem.ProductVariantId,
                ProductNameSnapshot = product.Name,
                VariantInfoSnapshot = variantInfoSnapshot,
                UnitPrice = unitPrice,
                Quantity = cartItem.Quantity,
                LineTotal = unitPrice * cartItem.Quantity
            };
            order.Items.Add(orderItem);

            // Create inventory transaction
            var transaction = new InventoryTransaction
            {
                ProductId = cartItem.ProductId,
                ChangeQuantity = -cartItem.Quantity,
                Reason = $"Order {orderNumber} (Guest)"
            };
            _context.InventoryTransactions.Add(transaction);
        }

        _context.Orders.Add(order);

        // Track voucher usage
        if (voucherId.HasValue && discountAmount > 0)
        {
            var usage = new VoucherUsage
            {
                VoucherId = voucherId.Value,
                OrderId = order.Id,
                GuestEmail = request.Email
            };
            _context.VoucherUsages.Add(usage);

            var voucher = await _context.Vouchers.FirstOrDefaultAsync(v => v.Id == voucherId.Value);
            if (voucher != null)
            {
                voucher.UsedCount += 1;
            }
        }
        await _context.SaveChangesAsync();

        try
        {
            var notificationDto = await BuildNewOrderNotificationDtoAsync(order, userIdForLookup: null, isGuest: true, CancellationToken.None);
            await _adminOrderNotificationPublisher.NotifyNewOrderAsync(notificationDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send admin new-order notification for guest order {OrderId}", order.Id);
        }

        // For COD orders, send confirmation email immediately
        if (request.PaymentMethod == PaymentMethod.COD)
        {
            try
            {
                _context.Entry(order).State = EntityState.Detached;
                
                var orderWithDetails = await _context.Orders
                    .AsNoTracking()
                    .Include(o => o.Items)
                        .ThenInclude(oi => oi.Product)
                            .ThenInclude(p => p.Images)
                    .Include(o => o.Items)
                        .ThenInclude(oi => oi.ProductVariant)
                    .FirstOrDefaultAsync(o => o.Id == order.Id);

                if (orderWithDetails != null && !string.IsNullOrWhiteSpace(orderWithDetails.GuestEmail))
                {
                    _logger.LogInformation("Sending order confirmation email for guest COD order {OrderId} to {Email}", 
                        order.Id, orderWithDetails.GuestEmail);
                    
                    await _emailService.SendGuestOrderConfirmationEmailAsync(orderWithDetails);
                    
                    _logger.LogInformation("Order confirmation email sent successfully for guest COD order {OrderId} to {Email}", 
                        order.Id, orderWithDetails.GuestEmail);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send order confirmation email for guest COD order {OrderId}. Error: {Error}", 
                    order.Id, ex.Message);
            }
        }

        return _mapper.Map<OrderDto>(order);
    }

    public async Task<List<OrderDto>> GetUserOrdersAsync(Guid userId)
    {
        var orders = await _context.Orders
            .Include(o => o.Items)
            .Where(o => o.UserId == userId)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

        return _mapper.Map<List<OrderDto>>(orders);
    }

    public async Task<OrderDto?> GetOrderByIdAsync(Guid orderId, Guid userId)
    {
        var order = await _context.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == userId);

        return order != null ? _mapper.Map<OrderDto>(order) : null;
    }

    public async Task<OrderDto?> GetGuestOrderByIdAsync(Guid orderId, string email)
    {
        var order = await _context.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId && o.GuestEmail == email && o.UserId == null);

        return order != null ? _mapper.Map<OrderDto>(order) : null;
    }

    public async Task<CreatePaymentResponse?> InitiateGuestPaymentAsync(Guid orderId, string email)
    {
        var order = await _context.Orders
            .FirstOrDefaultAsync(o => o.Id == orderId && o.GuestEmail == email && o.UserId == null);

        if (order == null)
        {
            throw new KeyNotFoundException("Không tìm thấy đơn hàng");
        }

        if (order.PaymentStatus != PaymentStatus.Pending)
        {
            throw new InvalidOperationException("Đơn hàng đã được thanh toán");
        }

        if (!order.PaymentMethod.HasValue)
        {
            throw new InvalidOperationException("Chưa chọn phương thức thanh toán");
        }

        // For COD orders, return response immediately without payment gateway
        if (order.PaymentMethod.Value == PaymentMethod.COD)
        {
            return new CreatePaymentResponse
            {
                PaymentUrl = null,
                OrderId = order.Id,
                OrderNumber = order.OrderNumber,
                IsCod = true
            };
        }

        // Build return URL (frontend) and notify URL (backend webhook)
        var frontendBaseUrl = "https://pickleball-shop-frontend.vercel.app";
        var returnUrl = $"{frontendBaseUrl}/payments/payos/return";
        var cancelUrl = $"{frontendBaseUrl}/payments/payos/cancel";
        var notifyUrl = $"/api/payments/{order.PaymentMethod.Value.ToString().ToLower()}/ipn";

        var paymentResponse = await _paymentService.CreatePaymentAsync(order, returnUrl, cancelUrl, notifyUrl);

        return paymentResponse;
    }

    public async Task<CreatePaymentResponse?> InitiatePaymentAsync(Guid orderId, Guid userId)
    {
        var order = await _context.Orders
            .Include(o => o.User)
            .FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == userId);

        if (order == null)
        {
            throw new KeyNotFoundException("Order not found");
        }

        if (order.PaymentStatus != PaymentStatus.Pending)
        {
            throw new InvalidOperationException("Order payment is already processed");
        }

        if (!order.PaymentMethod.HasValue)
        {
            throw new InvalidOperationException("Payment method not specified");
        }

        // For COD orders, return response immediately without payment gateway
        if (order.PaymentMethod.Value == PaymentMethod.COD)
        {
            return new CreatePaymentResponse
            {
                PaymentUrl = null,
                OrderId = order.Id,
                OrderNumber = order.OrderNumber,
                IsCod = true
            };
        }

        // Build return URL (frontend) and notify URL (backend webhook)
        var frontendBaseUrl = "https://pickleball-shop-frontend.vercel.app";
        var returnUrl = $"{frontendBaseUrl}/payments/payos/return";
        var cancelUrl = $"{frontendBaseUrl}/payments/payos/cancel";
        var notifyUrl = $"/api/payments/{order.PaymentMethod.Value.ToString().ToLower()}/ipn";

        var paymentResponse = await _paymentService.CreatePaymentAsync(order, returnUrl, cancelUrl, notifyUrl);

        return paymentResponse;
    }

    public async Task UpdateOrderPaymentStatusAsync(Guid orderId, string transactionId, bool isPaid, string? clientIpAddress = null)
    {
        // Load order to update payment status (tracked)
        var order = await _context.Orders
            .FirstOrDefaultAsync(o => o.Id == orderId);

        if (order == null)
        {
            throw new KeyNotFoundException("Order not found");
        }

        // Update payment status
        order.PaymentTransactionId = transactionId;
        order.PaymentStatus = isPaid ? PaymentStatus.Paid : PaymentStatus.Failed;

        if (isPaid)
        {
            // Khi thanh toán thành công, chuyển sang trạng thái "Đã xác nhận"
            order.OrderStatus = OrderStatus.Confirmed;
        }

        await _context.SaveChangesAsync();

        // Reload order after save with all navigation properties needed for email
        // Use AsNoTracking to get fresh data from database, similar to test endpoint
        var orderForEmail = await _context.Orders
            .AsNoTracking()
            .Include(o => o.User)
            .Include(o => o.Items)
                .ThenInclude(oi => oi.Product)
                    .ThenInclude(p => p.Images)
            .Include(o => o.Items)
                .ThenInclude(oi => oi.ProductVariant)
            .FirstOrDefaultAsync(o => o.Id == orderId);

        // Send order confirmation email when payment is successful
        if (isPaid && orderForEmail != null)
        {
            try
            {
                // Check if it's a guest order or registered user order
                if (orderForEmail.UserId == null && !string.IsNullOrWhiteSpace(orderForEmail.GuestEmail))
                {
                    // Guest order - use guest email method
                    _logger.LogInformation("Sending guest order confirmation email for order {OrderId} to {Email}", 
                        orderId, orderForEmail.GuestEmail);
                    
                    await _emailService.SendGuestOrderConfirmationEmailAsync(orderForEmail, clientIpAddress);
                    
                    _logger.LogInformation("Guest order confirmation email sent successfully for order {OrderId} to {Email}", 
                        orderId, orderForEmail.GuestEmail);
                }
                else if (orderForEmail.User != null && !string.IsNullOrWhiteSpace(orderForEmail.User.Email))
                {
                    // Registered user order
                    _logger.LogInformation("Sending order confirmation email for order {OrderId} to {Email}", 
                        orderId, orderForEmail.User.Email);
                    
                    await _emailService.SendOrderConfirmationEmailAsync(orderForEmail, clientIpAddress);
                    
                    _logger.LogInformation("Order confirmation email sent successfully for order {OrderId} to {Email}", 
                        orderId, orderForEmail.User.Email);
                }
                else
                {
                    _logger.LogWarning("Skipping order confirmation email for order {OrderId} - no valid email available. User: {User}, GuestEmail: {GuestEmail}", 
                        orderId, 
                        orderForEmail.User != null ? "exists" : "null",
                        orderForEmail.GuestEmail ?? "null");
                }
            }
            catch (Exception ex)
            {
                // Log error but don't fail the payment update
                // Email sending failure shouldn't prevent payment status update
                _logger.LogError(ex, "Failed to send order confirmation email for order {OrderId}. Error: {Error}", 
                    orderId, ex.Message);
            }
        }
        else if (isPaid && orderForEmail == null)
        {
            _logger.LogWarning("Cannot send order confirmation email: Order {OrderId} not found after payment update", orderId);
        }
    }

    public async Task<OrderDto> UpdateOrderStatusAsync(Guid orderId, OrderStatus status)
    {
        var order = await _context.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId);

        if (order == null)
        {
            throw new KeyNotFoundException("Không tìm thấy đơn hàng");
        }

        order.OrderStatus = status;
        await _context.SaveChangesAsync();

        return _mapper.Map<OrderDto>(order);
    }

    private async Task<NewOrderNotificationDto> BuildNewOrderNotificationDtoAsync(
        Order order,
        Guid? userIdForLookup,
        bool isGuest,
        CancellationToken cancellationToken)
    {
        string? email = null;
        string? name = null;

        if (isGuest)
        {
            email = order.GuestEmail;
            name = order.GuestFullName;
        }
        else if (userIdForLookup.HasValue)
        {
            var user = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userIdForLookup.Value, cancellationToken);

            if (user != null)
            {
                email = user.Email;
                name = user.FullName;
            }
        }

        return new NewOrderNotificationDto
        {
            OrderId = order.Id,
            OrderNumber = order.OrderNumber,
            TotalAmount = order.TotalAmount,
            CreatedAt = order.CreatedAt,
            IsGuestOrder = isGuest,
            CustomerEmail = email,
            CustomerName = name,
            PaymentMethod = order.PaymentMethod?.ToString()
        };
    }
}
