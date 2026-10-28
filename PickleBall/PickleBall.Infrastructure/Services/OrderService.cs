using PickleBall.Application.Mapping;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
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
    private readonly IConfiguration _configuration;

    public OrderService(
        ApplicationDbContext context,
        IMapper mapper,
        IPaymentService paymentService,
        IEmailService emailService,
        ILogger<OrderService> logger,
        IVoucherService voucherService,
        IAdminOrderNotificationPublisher adminOrderNotificationPublisher,
        IConfiguration configuration)
    {
        _configuration = configuration;
        _context = context;
        _mapper = mapper;
        _paymentService = paymentService;
        _emailService = emailService;
        _logger = logger;
        _voucherService = voucherService;
        _adminOrderNotificationPublisher = adminOrderNotificationPublisher;
    }

    private const int MaxQuantityPerLine = 100;
    private const int MaxLinesPerOrder = 50;

    private sealed record PricedLine(Product Product, ProductVariant? Variant, int Quantity, decimal UnitPrice);

    // ------------------------------------------------------------------------------------------------
    // Pricing helpers: the SERVER decides every monetary value from the database; the client only
    // supplies product ids, variant ids and quantities.
    // ------------------------------------------------------------------------------------------------

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    private static string GenerateOrderNumber()
    {
        // 48 bits of CSPRNG entropy: order numbers are not enumerable.
        var suffix = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(6));
        return $"ORD-{DateTime.UtcNow:yyyyMMdd}-{suffix}";
    }

    private static string? BuildVariantInfo(ProductVariant? variant)
    {
        if (variant == null) return null;

        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(variant.Color)) parts.Add($"Màu: {variant.Color}");
        if (!string.IsNullOrWhiteSpace(variant.Size)) parts.Add($"Size: {variant.Size}");
        if (!string.IsNullOrWhiteSpace(variant.Spec)) parts.Add($"Thông số: {variant.Spec}");
        return parts.Count > 0 ? string.Join(", ", parts) : null;
    }

    private static void EnsureValidQuantity(int quantity)
    {
        if (quantity < 1 || quantity > MaxQuantityPerLine)
        {
            throw new InvalidOperationException($"Số lượng phải từ 1 đến {MaxQuantityPerLine}");
        }
    }

    /// <summary>Resolves client-supplied ids into active products/variants priced from the database.</summary>
    private async Task<List<PricedLine>> ResolveGuestLinesAsync(IEnumerable<GuestCartItemDto>? items)
    {
        var requested = items?.ToList() ?? new List<GuestCartItemDto>();
        if (requested.Count == 0)
        {
            throw new InvalidOperationException("Giỏ hàng trống");
        }

        // Merge duplicate lines so quantity limits and stock checks apply to the real total.
        var merged = requested
            .GroupBy(i => (i.ProductId, VariantId: i.ProductVariantId))
            .Select(g => (g.Key.ProductId, g.Key.VariantId, Quantity: g.Sum(x => (long)x.Quantity)))
            .ToList();

        if (merged.Count > MaxLinesPerOrder)
        {
            throw new InvalidOperationException("Giỏ hàng có quá nhiều sản phẩm");
        }

        var productIds = merged.Select(m => m.ProductId).Distinct().ToList();
        var products = await _context.Products
            .Include(p => p.Variants)
            .Where(p => productIds.Contains(p.Id) && p.IsActive)
            .ToListAsync();

        var lines = new List<PricedLine>();
        foreach (var m in merged)
        {
            if (m.Quantity < 1 || m.Quantity > MaxQuantityPerLine)
            {
                throw new InvalidOperationException($"Số lượng phải từ 1 đến {MaxQuantityPerLine}");
            }

            var product = products.FirstOrDefault(p => p.Id == m.ProductId)
                ?? throw new InvalidOperationException("Sản phẩm không tồn tại hoặc đã ngừng bán");

            lines.Add(BuildLine(product, m.VariantId, (int)m.Quantity));
        }

        return lines;
    }

    private static PricedLine BuildLine(Product product, Guid? variantId, int quantity)
    {
        if (product.HasVariants)
        {
            if (!variantId.HasValue)
            {
                throw new InvalidOperationException($"Vui lòng chọn biến thể cho sản phẩm {product.Name}");
            }

            var variant = product.Variants.FirstOrDefault(v => v.Id == variantId.Value && v.IsActive)
                ?? throw new InvalidOperationException("Biến thể sản phẩm không tồn tại hoặc đã ngừng bán");

            return new PricedLine(product, variant, quantity, variant.Price);
        }

        if (variantId.HasValue)
        {
            throw new InvalidOperationException($"Sản phẩm {product.Name} không có biến thể");
        }

        return new PricedLine(product, null, quantity, product.Price);
    }

    public async Task<decimal> CalculateGuestSubtotalAsync(IEnumerable<GuestCartItemDto> items)
    {
        var lines = await ResolveGuestLinesAsync(items);
        return lines.Sum(l => l.UnitPrice * l.Quantity);
    }

    private async Task<List<PricedLine>> ResolveCartLinesAsync(Guid userId)
    {
        var cart = await _context.Carts
            .Include(c => c.Items)
                .ThenInclude(ci => ci.Product)
                    .ThenInclude(p => p.Variants)
            .FirstOrDefaultAsync(c => c.UserId == userId);

        if (cart == null || !cart.Items.Any())
        {
            throw new InvalidOperationException("Cart is empty");
        }

        if (cart.Items.Count > MaxLinesPerOrder)
        {
            throw new InvalidOperationException("Cart has too many items");
        }

        var lines = new List<PricedLine>();
        foreach (var item in cart.Items)
        {
            EnsureValidQuantity(item.Quantity);

            if (!item.Product.IsActive)
            {
                throw new InvalidOperationException($"Sản phẩm {item.Product.Name} đã ngừng bán");
            }

            // Re-price from the current catalog, NOT from the stale snapshot taken when the item was added.
            lines.Add(BuildLine(item.Product, item.ProductVariantId, item.Quantity));
        }

        return lines;
    }

    public async Task<decimal> CalculateCartSubtotalAsync(Guid userId)
    {
        var lines = await ResolveCartLinesAsync(userId);
        return lines.Sum(l => l.UnitPrice * l.Quantity);
    }

    /// <summary>
    /// Atomically reserves stock: the decrement only succeeds when enough stock remains, so concurrent
    /// checkouts can never oversell. Falls back to tracked updates for non-relational providers (tests/dev).
    /// </summary>
    private async Task ReserveStockAsync(PricedLine line)
    {
        var qty = line.Quantity;
        bool reserved;

        if (_context.Database.IsRelational())
        {
            var rows = line.Variant != null
                ? await _context.ProductVariants
                    .Where(v => v.Id == line.Variant.Id && v.StockQuantity >= qty)
                    .ExecuteUpdateAsync(s => s.SetProperty(v => v.StockQuantity, v => v.StockQuantity - qty))
                : await _context.Products
                    .Where(p => p.Id == line.Product.Id && p.StockQuantity >= qty)
                    .ExecuteUpdateAsync(s => s.SetProperty(p => p.StockQuantity, p => p.StockQuantity - qty));
            reserved = rows == 1;
        }
        else if (line.Variant != null)
        {
            reserved = line.Variant.StockQuantity >= qty;
            if (reserved) line.Variant.StockQuantity -= qty;
        }
        else
        {
            reserved = line.Product.StockQuantity >= qty;
            if (reserved) line.Product.StockQuantity -= qty;
        }

        if (!reserved)
        {
            throw new InvalidOperationException($"Sản phẩm {line.Product.Name} không đủ số lượng tồn kho");
        }
    }

    /// <summary>Consumes one use of the voucher atomically (cannot exceed the total usage limit).</summary>
    private async Task ConsumeVoucherAsync(Guid voucherId)
    {
        bool ok;
        if (_context.Database.IsRelational())
        {
            var rows = await _context.Vouchers
                .Where(v => v.Id == voucherId && (v.UsageLimitTotal == null || v.UsedCount < v.UsageLimitTotal))
                .ExecuteUpdateAsync(s => s.SetProperty(v => v.UsedCount, v => v.UsedCount + 1));
            ok = rows == 1;
        }
        else
        {
            var voucher = await _context.Vouchers.FirstAsync(v => v.Id == voucherId);
            ok = voucher.UsageLimitTotal == null || voucher.UsedCount < voucher.UsageLimitTotal;
            if (ok) voucher.UsedCount += 1;
        }

        if (!ok)
        {
            throw new InvalidOperationException("Mã giảm giá đã được sử dụng tối đa số lần cho phép");
        }
    }

    /// <summary>
    /// Runs the unit of work in a single transaction (when the provider supports it) wrapped in the
    /// provider's execution strategy so stock/voucher updates and the order are all-or-nothing.
    /// </summary>
    private async Task<T> InTransactionAsync<T>(Func<Task<T>> work)
    {
        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            _context.ChangeTracker.Clear();
            await using var tx = _context.Database.IsRelational()
                ? await _context.Database.BeginTransactionAsync()
                : null;
            try
            {
                var result = await work();
                if (tx != null) await tx.CommitAsync();
                return result;
            }
            catch
            {
                if (tx != null) await tx.RollbackAsync();
                _context.ChangeTracker.Clear();
                throw;
            }
        });
    }

    private static ShippingAddressDto RequireShipping(ShippingAddressDto? address)
        => address ?? throw new InvalidOperationException("Địa chỉ giao hàng là bắt buộc");

    public async Task<OrderDto> CreateOrderAsync(Guid userId, CreateOrderRequest request)
    {
        var shipping = RequireShipping(request.ShippingAddress);

        var order = await InTransactionAsync(async () =>
        {
            var lines = await ResolveCartLinesAsync(userId);
            var orderNumber = GenerateOrderNumber();

            var subtotal = lines.Sum(l => l.UnitPrice * l.Quantity);
            decimal discountAmount = 0;
            Guid? voucherId = null;

            if (!string.IsNullOrWhiteSpace(request.VoucherCode))
            {
                var validateResult = await _voucherService.ValidateAndCalculateAsync(
                    request.VoucherCode, subtotal, userId, null);

                if (!validateResult.isValid)
                {
                    throw new InvalidOperationException(validateResult.message);
                }

                discountAmount = validateResult.discountAmount;
                voucherId = validateResult.voucher?.Id;
            }

            var newOrder = new Order
            {
                OrderNumber = orderNumber,
                UserId = userId,
                TotalAmount = Math.Max(0, subtotal - discountAmount),
                PaymentStatus = PaymentStatus.Pending,
                OrderStatus = OrderStatus.Processing,
                PaymentMethod = request.PaymentMethod,
                ShippingFullName = shipping.FullName,
                ShippingPhoneNumber = shipping.PhoneNumber,
                ShippingAddressLine = shipping.AddressLine,
                ShippingWard = shipping.Ward,
                ShippingDistrict = shipping.District,
                ShippingCity = shipping.City,
                VoucherId = voucherId,
                VoucherDiscountAmount = discountAmount > 0 ? discountAmount : null
            };

            await AddLinesAndReserveStockAsync(newOrder, lines, orderNumber, isGuest: false);

            _context.Orders.Add(newOrder);

            if (voucherId.HasValue && discountAmount > 0)
            {
                await ConsumeVoucherAsync(voucherId.Value);
                _context.VoucherUsages.Add(new VoucherUsage
                {
                    VoucherId = voucherId.Value,
                    OrderId = newOrder.Id,
                    UserId = userId
                });
            }

            // Clear cart
            var cartItems = await _context.CartItems.Where(ci => ci.Cart.UserId == userId).ToListAsync();
            _context.CartItems.RemoveRange(cartItems);

            await _context.SaveChangesAsync();
            return newOrder;
        });

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
                var orderWithDetails = await _context.Orders
                    .AsNoTracking()
                    .Include(o => o.User)
                    .Include(o => o.Items)
                        .ThenInclude(oi => oi.Product)
                            .ThenInclude(p => p.Images)
                    .Include(o => o.Items)
                        .ThenInclude(oi => oi.ProductVariant)
                    .FirstOrDefaultAsync(o => o.Id == order.Id);

                if (orderWithDetails?.User != null && !string.IsNullOrWhiteSpace(orderWithDetails.User.Email))
                {
                    await _emailService.SendOrderConfirmationEmailAsync(orderWithDetails);
                }
                else
                {
                    _logger.LogWarning("Cannot send order confirmation email for COD order {OrderId}: user email not available", order.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send order confirmation email for COD order {OrderId}", order.Id);
            }
        }

        return _mapper.Map<OrderDto>(order);
    }

    private async Task AddLinesAndReserveStockAsync(Order order, List<PricedLine> lines, string orderNumber, bool isGuest)
    {
        foreach (var line in lines)
        {
            order.Items.Add(new OrderItem
            {
                OrderId = order.Id,
                ProductId = line.Product.Id,
                ProductVariantId = line.Variant?.Id,
                ProductNameSnapshot = line.Product.Name,
                VariantInfoSnapshot = BuildVariantInfo(line.Variant),
                UnitPrice = line.UnitPrice,
                Quantity = line.Quantity,
                LineTotal = line.UnitPrice * line.Quantity
            });

            await ReserveStockAsync(line);

            _context.InventoryTransactions.Add(new InventoryTransaction
            {
                ProductId = line.Product.Id,
                ChangeQuantity = -line.Quantity,
                Reason = isGuest ? $"Order {orderNumber} (Guest)" : $"Order {orderNumber}"
            });
        }
    }

    public async Task<OrderDto> CreateGuestOrderAsync(CreateGuestOrderRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            throw new InvalidOperationException("Email là bắt buộc");
        }

        var shipping = RequireShipping(request.ShippingAddress);
        var guestEmail = NormalizeEmail(request.Email);

        var order = await InTransactionAsync(async () =>
        {
            var lines = await ResolveGuestLinesAsync(request.Items);
            var orderNumber = GenerateOrderNumber();

            var subtotal = lines.Sum(l => l.UnitPrice * l.Quantity);
            decimal discountAmount = 0;
            Guid? voucherId = null;

            if (!string.IsNullOrWhiteSpace(request.VoucherCode))
            {
                var validateResult = await _voucherService.ValidateAndCalculateAsync(
                    request.VoucherCode, subtotal, null, guestEmail);

                if (!validateResult.isValid)
                {
                    throw new InvalidOperationException(validateResult.message);
                }

                discountAmount = validateResult.discountAmount;
                voucherId = validateResult.voucher?.Id;
            }

            var newOrder = new Order
            {
                OrderNumber = orderNumber,
                UserId = null,
                GuestEmail = guestEmail,
                GuestFullName = request.FullName?.Trim(),
                TotalAmount = Math.Max(0, subtotal - discountAmount),
                PaymentStatus = PaymentStatus.Pending,
                OrderStatus = OrderStatus.Processing,
                PaymentMethod = request.PaymentMethod,
                ShippingFullName = shipping.FullName,
                ShippingPhoneNumber = shipping.PhoneNumber,
                ShippingAddressLine = shipping.AddressLine,
                ShippingWard = shipping.Ward,
                ShippingDistrict = shipping.District,
                ShippingCity = shipping.City,
                VoucherId = voucherId,
                VoucherDiscountAmount = discountAmount > 0 ? discountAmount : null
            };

            await AddLinesAndReserveStockAsync(newOrder, lines, orderNumber, isGuest: true);

            _context.Orders.Add(newOrder);

            if (voucherId.HasValue && discountAmount > 0)
            {
                await ConsumeVoucherAsync(voucherId.Value);
                _context.VoucherUsages.Add(new VoucherUsage
                {
                    VoucherId = voucherId.Value,
                    OrderId = newOrder.Id,
                    GuestEmail = guestEmail
                });
            }

            await _context.SaveChangesAsync();
            return newOrder;
        });

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
                    await _emailService.SendGuestOrderConfirmationEmailAsync(orderWithDetails);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send order confirmation email for guest COD order {OrderId}", order.Id);
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
        // Ownership is enforced in the query itself (BOLA protection).
        var order = await _context.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == userId);

        return order != null ? _mapper.Map<OrderDto>(order) : null;
    }

    public async Task<OrderDto?> GetGuestOrderByIdAsync(Guid orderId, string email)
    {
        var normalized = NormalizeEmail(email);
        var order = await _context.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == null &&
                                      o.GuestEmail != null && o.GuestEmail.ToLower() == normalized);

        return order != null ? _mapper.Map<OrderDto>(order) : null;
    }

    public async Task<OrderDto?> GetGuestOrderAsync(string orderNumber, string email)
    {
        var normalized = NormalizeEmail(email);
        var number = orderNumber.Trim();
        var order = await _context.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.OrderNumber == number && o.UserId == null &&
                                      o.GuestEmail != null && o.GuestEmail.ToLower() == normalized);

        return order != null ? _mapper.Map<OrderDto>(order) : null;
    }

    public async Task<CreatePaymentResponse?> InitiateGuestPaymentAsync(Guid orderId, string email)
    {
        var normalizedEmail = NormalizeEmail(email);
        var order = await _context.Orders
            .FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == null &&
                                      o.GuestEmail != null && o.GuestEmail.ToLower() == normalizedEmail);

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
        var frontendBaseUrl = GetFrontendBaseUrl();
        var returnUrl = $"{frontendBaseUrl}/payments/payos/return";
        var cancelUrl = $"{frontendBaseUrl}/payments/payos/cancel";
        var notifyUrl = $"/api/payments/{order.PaymentMethod.Value.ToString().ToLower()}/ipn";

        var paymentResponse = await _paymentService.CreatePaymentAsync(order, returnUrl, cancelUrl, notifyUrl);

        // Persist the gateway order code (PayOS) so webhooks/confirmations can be matched server-side.
        await _context.SaveChangesAsync();

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
        var frontendBaseUrl = GetFrontendBaseUrl();
        var returnUrl = $"{frontendBaseUrl}/payments/payos/return";
        var cancelUrl = $"{frontendBaseUrl}/payments/payos/cancel";
        var notifyUrl = $"/api/payments/{order.PaymentMethod.Value.ToString().ToLower()}/ipn";

        var paymentResponse = await _paymentService.CreatePaymentAsync(order, returnUrl, cancelUrl, notifyUrl);

        // Persist the gateway order code (PayOS) so webhooks/confirmations can be matched server-side.
        await _context.SaveChangesAsync();

        return paymentResponse;
    }

    private string GetFrontendBaseUrl()
    {
        var url = _configuration["Email:FrontendUrl"];
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out _))
        {
            url = "https://pickleball-shop-frontend.vercel.app";
        }
        return url.TrimEnd('/');
    }

    public async Task<bool> MarkPaymentFailedAsync(Guid orderId, string transactionId)
    {
        var order = await _context.Orders.FirstOrDefaultAsync(o => o.Id == orderId);
        if (order == null)
        {
            throw new KeyNotFoundException("Order not found");
        }

        // Monotonic: a Paid (or Refunded) order can never be downgraded by a late/replayed failure callback.
        if (order.PaymentStatus != PaymentStatus.Pending)
        {
            return false;
        }

        order.PaymentTransactionId = transactionId;
        order.PaymentStatus = PaymentStatus.Failed;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ConfirmPaymentAsync(
        Guid orderId,
        PaymentMethod gateway,
        string transactionId,
        decimal paidAmount,
        string currency,
        string? rawResponse,
        string? clientIpAddress = null)
    {
        var order = await _context.Orders.FirstOrDefaultAsync(o => o.Id == orderId);
        if (order == null)
        {
            throw new KeyNotFoundException("Order not found");
        }

        // Idempotent / replay-safe: only a Pending or Failed order can transition to Paid, exactly once.
        if (order.PaymentStatus == PaymentStatus.Paid || order.PaymentStatus == PaymentStatus.Refunded)
        {
            return false;
        }

        if (order.OrderStatus == OrderStatus.Cancelled)
        {
            _logger.LogWarning("Ignoring payment confirmation for cancelled order {OrderId}", orderId);
            return false;
        }

        // The amount that was really paid must match what the SERVER computed for this order.
        if (paidAmount != order.TotalAmount || !string.Equals(currency, "VND", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "Payment amount/currency mismatch for order {OrderId}: expected {Expected} VND, got {Paid} {Currency}",
                orderId, order.TotalAmount, paidAmount, currency);
            return false;
        }

        order.PaymentTransactionId = transactionId;
        order.PaymentStatus = PaymentStatus.Paid;
        if (order.OrderStatus == OrderStatus.Processing)
        {
            // Khi thanh toán thành công, chuyển sang trạng thái "Đã xác nhận"
            order.OrderStatus = OrderStatus.Confirmed;
        }

        _context.Payments.Add(new Payment
        {
            OrderId = order.Id,
            PaymentGateway = gateway,
            Amount = paidAmount,
            Currency = "VND",
            TransactionId = transactionId,
            Status = PaymentStatus.Paid,
            RawResponse = rawResponse
        });

        await _context.SaveChangesAsync();

        await SendPaymentConfirmationEmailAsync(orderId, clientIpAddress);
        return true;
    }

    private async Task SendPaymentConfirmationEmailAsync(Guid orderId, string? clientIpAddress)
    {
        const bool isPaid = true;

        // Reload order after save with all navigation properties needed for email
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
