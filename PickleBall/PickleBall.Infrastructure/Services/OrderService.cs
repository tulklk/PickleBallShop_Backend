using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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

    public OrderService(ApplicationDbContext context, IMapper mapper, IPaymentService paymentService, IEmailService emailService, ILogger<OrderService> logger)
    {
        _context = context;
        _mapper = mapper;
        _paymentService = paymentService;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task<OrderDto> CreateOrderAsync(Guid userId, CreateOrderRequest request)
    {
        // Get user cart
        var cart = await _context.Carts
            .Include(c => c.Items)
                .ThenInclude(ci => ci.Product)
            .FirstOrDefaultAsync(c => c.UserId == userId);

        if (cart == null || !cart.Items.Any())
        {
            throw new InvalidOperationException("Cart is empty");
        }

        // Validate stock
        foreach (var item in cart.Items)
        {
            if (item.Product.StockQuantity < item.Quantity)
            {
                throw new InvalidOperationException($"Insufficient stock for product {item.Product.Name}");
            }
        }

        // Generate order number
        var orderNumber = $"ORD-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpper()}";

        // Calculate total
        var totalAmount = cart.Items.Sum(ci => ci.UnitPriceSnapshot * ci.Quantity);

        // Create order
        var order = new Order
        {
            OrderNumber = orderNumber,
            UserId = userId,
            TotalAmount = totalAmount,
            PaymentStatus = PaymentStatus.Pending,
            // For COD orders, set order status to Confirmed since payment will be collected on delivery
            OrderStatus = request.PaymentMethod == PaymentMethod.COD ? OrderStatus.Confirmed : OrderStatus.Pending,
            PaymentMethod = request.PaymentMethod,
            ShippingFullName = request.ShippingAddress.FullName,
            ShippingPhoneNumber = request.ShippingAddress.PhoneNumber,
            ShippingAddressLine = request.ShippingAddress.AddressLine,
            ShippingWard = request.ShippingAddress.Ward,
            ShippingDistrict = request.ShippingAddress.District,
            ShippingCity = request.ShippingAddress.City
        };

        // Create order items
        foreach (var cartItem in cart.Items)
        {
            var orderItem = new OrderItem
            {
                OrderId = order.Id,
                ProductId = cartItem.ProductId,
                ProductNameSnapshot = cartItem.Product.Name,
                UnitPrice = cartItem.UnitPriceSnapshot,
                Quantity = cartItem.Quantity,
                LineTotal = cartItem.UnitPriceSnapshot * cartItem.Quantity
            };
            order.Items.Add(orderItem);

            // Update inventory
            cartItem.Product.StockQuantity -= cartItem.Quantity;

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

        // Clear cart
        _context.CartItems.RemoveRange(cart.Items);

        await _context.SaveChangesAsync();

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
        // Load order with all navigation properties needed for email
        // If order is already tracked, EF Core will use the tracked entity
        var order = await _context.Orders
            .Include(o => o.User)
            .Include(o => o.Items)
                .ThenInclude(oi => oi.Product)
                    .ThenInclude(p => p.Images)
            .FirstOrDefaultAsync(o => o.Id == orderId);

        if (order == null)
        {
            throw new KeyNotFoundException("Order not found");
        }

        order.PaymentTransactionId = transactionId;
        order.PaymentStatus = isPaid ? PaymentStatus.Paid : PaymentStatus.Failed;

        if (isPaid)
        {
            order.OrderStatus = OrderStatus.Confirmed;
        }

        await _context.SaveChangesAsync();

        // Send order confirmation email when payment is successful
        if (isPaid)
        {
            try
            {
                await _emailService.SendOrderConfirmationEmailAsync(order, clientIpAddress);
            }
            catch (Exception ex)
            {
                // Log error but don't fail the payment update
                // Email sending failure shouldn't prevent payment status update
                _logger.LogError(ex, "Failed to send order confirmation email for order {OrderId}", orderId);
            }
        }
    }
}
