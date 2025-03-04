using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text;
using DinkToPdf;
using DinkToPdf.Contracts;
using reactBackend.Data;
using reactBackend.Models;
using reactBackend.Models.Enums;
using reactBackend.Dtos;
using reactBackend.Services;


namespace reactBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class OrdersController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<OrdersController> _logger;
        private readonly IConverter _pdfConverter;
        private readonly IWebHostEnvironment _environment;
        private readonly IPaymentService _paymentService;

        public OrdersController(
            ApplicationDbContext context,
            ILogger<OrdersController> logger,
            IConverter pdfConverter,
            IWebHostEnvironment environment,
            IPaymentService paymentService)
        {
            _context = context;
            _logger = logger;
            _pdfConverter = pdfConverter;
            _environment = environment;
            _paymentService = paymentService;
        }

        [HttpPost]
        public async Task<ActionResult<OrderResponseDto>> CreateOrder([FromBody] CreateOrderDto dto)
        {
            if (!Enum.TryParse<PaymentMethodType>(dto.PaymentMethod.ToString(), out var paymentMethod))
            {
                return Problem(
                    statusCode: 400,
                    title: "خطأ في البيانات",
                    detail: "طريقة الدفع غير صحيحة"
                );
            }

            // تحديث dto.PaymentMethod بالقيمة الرقمية المناسبة
            dto.PaymentMethod = paymentMethod;

            // 1. التحقق من المستخدم
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId == null)
                return Problem(
                    statusCode: 401,
                    title: "غير مصرح",
                    detail: "لم يتم العثور على المستخدم."
                );

            // 2. التحقق من صحة بيانات العنوان
            if (dto?.Address == null ||
                string.IsNullOrWhiteSpace(dto.Address.FullName) ||
                string.IsNullOrWhiteSpace(dto.Address.PhoneNumber) ||
                string.IsNullOrWhiteSpace(dto.Address.City))
            {
                return Problem(
                    statusCode: 400,
                    title: "خطأ في البيانات",
                    detail: "بيانات العنوان غير مكتملة"
                );
            }

            // 3. التحقق من طريقة الدفع
            if (!Enum.IsDefined(typeof(PaymentMethodType), dto.PaymentMethod))
            {
                return Problem(
                    statusCode: 400,
                    title: "خطأ في البيانات",
                    detail: "طريقة الدفع غير صحيحة"
                );
            }

            var strategy = _context.Database.CreateExecutionStrategy();

            return await strategy.ExecuteAsync(async () =>
            {
                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    _logger.LogInformation($"Creating order for user {userId}");

                    // 4. جلب عناصر السلة مع التحقق من وجودها
                    var cartItems = await _context.CartItems
                        .Where(c => c.UserId == userId)
                        .Include(c => c.Product)
                        .ToListAsync();

                    if (!cartItems.Any())
                    {
                        return Problem(
                            statusCode: 400,
                            title: "خطأ في الطلب",
                            detail: "السلة فارغة"
                        );
                    }

                    // 5. التحقق من المخزون لكل منتج
                    foreach (var item in cartItems)
                    {
                        // التحقق من وجود المنتج
                        if (item.Product == null)
                        {
                            return Problem(
                                statusCode: 400,
                                title: "خطأ في المنتج",
                                detail: "أحد المنتجات غير متوفر"
                            );
                        }

                        // التحقق من المخزون مباشرة من قاعدة البيانات
                        var currentStock = await _context.Products
                            .Where(p => p.Id == item.ProductId)
                            .Select(p => new { p.Stock, p.Name })
                            .FirstOrDefaultAsync();

                        if (currentStock == null || currentStock.Stock < item.Quantity)
                        {
                            await transaction.RollbackAsync();
                            return Problem(
                                statusCode: 400,
                                title: "خطأ في المخزون",
                                detail: $"المنتج {item.Product.Name} غير متوفر بالكمية المطلوبة (المتوفر: {currentStock?.Stock ?? 0})"
                            );
                        }
                    }

                    // 6. جلب الخصومات النشطة
                    var now = DateTime.UtcNow;
                    var activeDiscounts = await _context.Discounts
                        .Where(d => d.IsActive && d.StartDate <= now && d.EndDate >= now)
                        .Include(d => d.Products)
                        .ToListAsync();

                    // 7. حساب المبالغ مع تطبيق الخصومات
                    decimal subtotal = 0;
                    decimal totalDiscount = 0;
                    var orderItems = new List<OrderItem>();

                    foreach (var item in cartItems)
                    {
                        if (item.Product == null) continue;

                        // حساب السعر الأصلي
                        decimal originalPrice = item.Product.Price;
                        decimal originalTotal = originalPrice * item.Quantity;
                        subtotal += originalTotal;

                        // البحث عن الخصومات المطبقة على هذا المنتج
                        var applicableDiscounts = activeDiscounts.Where(d =>
                            d.Scope == DiscountScope.AllProducts ||
                            (d.Scope == DiscountScope.Category && d.CategoryName == item.Product.Category) ||
                            (d.Scope == DiscountScope.Product && d.Products != null && d.Products.Any(dp => dp.ProductId == item.ProductId))
                        ).ToList();

                        decimal finalPrice = originalPrice;
                        Discount? appliedDiscount = null;
                        decimal discountAmount = 0;

                        if (applicableDiscounts.Any())
                        {
                            // اختيار أفضل خصم (الأعلى قيمة)
                            appliedDiscount = GetBestDiscount(applicableDiscounts, originalPrice);

                            // حساب السعر بعد الخصم
                            finalPrice = CalculateDiscountedPrice(originalPrice, appliedDiscount);

                            // حساب مبلغ الخصم للوحدة الواحدة ثم للكمية الكلية
                            decimal unitDiscountAmount = originalPrice - finalPrice;
                            discountAmount = unitDiscountAmount * item.Quantity;

                            totalDiscount += discountAmount;
                        }

                        // إنشاء عنصر الطلب
                        var orderItem = new OrderItem
                        {
                            ProductId = item.ProductId,
                            ProductName = item.Product.Name ?? "", // استخدام اسم المنتج
                            Quantity = item.Quantity,
                            Price = finalPrice,  // السعر بعد الخصم (للوحدة)
                            OriginalPrice = originalPrice,  // السعر الأصلي (قبل الخصم)
                            DiscountAmount = discountAmount,  // مبلغ الخصم الإجمالي للكمية
                            Total = finalPrice * item.Quantity  // المجموع بعد الخصم
                        };

                        // إضافة معلومات الخصم إذا كان موجوداً
                        if (appliedDiscount != null)
                        {
                            orderItem.DiscountName = appliedDiscount.Name; // تعيين اسم الخصم
                            orderItem.DiscountId = appliedDiscount.Id;
                        }

                        orderItems.Add(orderItem);
                    }

                    decimal vat = Math.Round((subtotal - totalDiscount) * 0.15m / 1.15m, 2); // 15% من السعر الشامل للضريبة
                    decimal total = subtotal - totalDiscount; // هذا هو المجموع الكلي ويشمل الضريبة
                    decimal deliveryFee = 25m;

                    // 8. إنشاء الطلب
                    var order = new Order
                    {
                        UserId = userId,
                        Status = OrderStatus.Pending,
                        SubTotal = subtotal,
                        DiscountAmount = totalDiscount,  // إضافة مبلغ الخصم
                        VatAmount = vat,
                        TotalAmount = total,
                        DeliveryFee = deliveryFee,
                        OrderDate = DateTime.UtcNow,
                        Items = orderItems,
                        Address = new DeliveryAddress
                        {
                            FullName = dto.Address.FullName.Trim(),
                            PhoneNumber = dto.Address.PhoneNumber.Trim(),
                            City = dto.Address.City.Trim(),
                            Street = dto.Address.Street?.Trim(),
                            BuildingNumber = dto.Address.BuildingNumber?.Trim(),
                            AdditionalDetails = dto.Address.AdditionalDetails?.Trim()
                        }
                    };

                    _context.Orders.Add(order);
                    await _context.SaveChangesAsync();

                    // 9. إنشاء تفاصيل الدفع
                    var paymentDetails = new PaymentDetails
                    {
                        OrderId = order.Id,
                        PaymentMethod = dto.PaymentMethod,
                        Status = PaymentStatus.Pending,
                        Amount = order.FinalAmount,  // استخدام FinalAmount الذي يتضمن الخصومات
                        Currency = "SAR",
                        CreatedAt = DateTime.UtcNow,
                        PaymentData = dto.PaymentDetails ?? new Dictionary<string, string>()
                    };

                    _context.PaymentDetails.Add(paymentDetails);
                    order.PaymentDetails = paymentDetails;

                    // 10. معالجة الدفع
                    var paymentResponse = await ProcessPaymentAsync(paymentDetails);
                    if (!paymentResponse.IsSuccess)
                    {
                        await transaction.RollbackAsync();
                        return Problem(
                            statusCode: 400,
                            title: "خطأ في الدفع",
                            detail: paymentResponse.ErrorMessage
                        );
                    }

                    // 11. تحديث المخزون
                    foreach (var item in cartItems)
                    {
                        if (item.Product != null)
                        {
                            item.Product.Stock -= item.Quantity;
                            _context.Entry(item.Product).State = EntityState.Modified;
                        }
                    }

                    // 12. إنشاء الفاتورة
                    try
                    {
                        await GenerateInvoiceForOrder(order);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Failed to generate invoice for order {order.Id}: {ex.Message}");
                        // لا نريد إيقاف العملية إذا فشل إنشاء الفاتورة
                    }

                    // 13. حذف عناصر السلة
                    _context.CartItems.RemoveRange(cartItems);
                    await _context.SaveChangesAsync();

                    // 14. إعداد DTO للاستجابة
                    var responseDto = new OrderResponseDto
                    {
                        Id = order.Id,
                        OrderDate = order.OrderDate,
                        Status = order.Status.ToString(),
                        SubTotal = order.SubTotal,
                        DiscountAmount = order.DiscountAmount,  // إضافة معلومات الخصم
                        VatAmount = order.VatAmount,
                        TotalAmount = order.TotalAmount,
                        DeliveryFee = order.DeliveryFee,
                        FinalAmount = order.FinalAmount,
                        PaymentStatus = paymentDetails.Status.ToString(),
                        PaymentMethod = paymentDetails.PaymentMethod.ToString(),
                        PaymentDetails = new PaymentDetailsDto
                        {
                            PaymentMethod = paymentDetails.PaymentMethod.ToString(),
                            Status = paymentDetails.Status.ToString(),
                            PaidAt = paymentDetails.PaidAt,
                            TransactionId = paymentDetails.TransactionId,
                            ErrorMessage = paymentDetails.ErrorMessage,
                            IsRefunded = paymentDetails.IsRefunded,
                            RefundedAt = paymentDetails.RefundedAt,
                            RefundAmount = paymentDetails.RefundAmount
                        },
                        Items = order.Items.Select(i => new OrderItemDto
                        {
                            ProductId = i.ProductId,
                            ProductName = i.ProductName ?? i.Product?.Name ?? "",
                            Quantity = i.Quantity,
                            Price = i.Price,
                            OriginalPrice = i.OriginalPrice,
                            DiscountAmount = i.DiscountAmount,
                            Total = i.Total
                        }).ToList(),
                        DeliveryAddress = new DeliveryAddressDto
                        {
                            FullName = order.Address.FullName,
                            PhoneNumber = order.Address.PhoneNumber,
                            City = order.Address.City,
                            Street = order.Address.Street,
                            BuildingNumber = order.Address.BuildingNumber,
                            AdditionalDetails = order.Address.AdditionalDetails
                        },
                        HasDiscount = totalDiscount > 0  // إضافة معلومات وجود خصم
                    };

                    // 15. إنهاء المعاملة
                    await transaction.CommitAsync();
                    return CreatedAtAction(nameof(GetOrder), new { id = order.Id }, responseDto);
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Error creating order for user {UserId}", userId);
                    return Problem(
                        statusCode: 500,
                        title: "خطأ في النظام",
                        detail: "حدث خطأ غير متوقع أثناء معالجة الطلب"
                    );
                }
            });
        }

        // 1. إحصائيات الطلبات والمبيعات
        [HttpGet("statistics")]
        [Authorize]
        public async Task<ActionResult<object>> GetOrdersStatistics(
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            try
            {
                var query = _context.Orders.AsQueryable();

                if (startDate.HasValue)
                    query = query.Where(o => o.OrderDate >= startDate.Value);
                if (endDate.HasValue)
                    query = query.Where(o => o.OrderDate <= endDate.Value);

                // استخدام ToList() قبل عمليات التجميع لتجنب مشكلة LINQ Translation
                var orders = await query.ToListAsync();

                var stats = new
                {
                    totalOrders = orders.Count,
                    completedOrders = orders.Count(o => o.Status == OrderStatus.Delivered),
                    pendingOrders = orders.Count(o => o.Status == OrderStatus.Pending),
                    processingOrders = orders.Count(o => o.Status == OrderStatus.Processing),
                    cancelledOrders = orders.Count(o => o.Status == OrderStatus.Cancelled),
                    totalRevenue = orders.Sum(o => o.FinalAmount),
                    averageOrderValue = orders.Any() ? orders.Average(o => o.FinalAmount) : 0,
                    dailyOrders = orders.GroupBy(o => o.OrderDate.Date)
                                       .Select(g => new
                                       {
                                           date = g.Key.ToString("yyyy-MM-dd"),
                                           count = g.Count(),
                                           revenue = g.Sum(o => o.FinalAmount)
                                       })
                                       .OrderBy(x => x.date)
                                       .ToList()
                };

                return Ok(stats);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "An error occurred", message = ex.Message });
            }
        }

        [HttpGet("top-customers")]
        [Authorize]
        public async Task<ActionResult<object>> GetTopCustomers([FromQuery] int limit = 10)
        {
            try
            {
                var orders = await _context.Orders
                    .Include(o => o.User)
                    .Where(o => o.Status != OrderStatus.Cancelled)
                    .ToListAsync();

                var topCustomers = orders
                    .GroupBy(o => new { o.UserId, Name = o.User.Name, Email = o.User.Email })
                    .Select(g => new
                    {
                        userId = g.Key.UserId,
                        name = g.Key.Name,
                        email = g.Key.Email,
                        ordersCount = g.Count(),
                        totalSpent = g.Sum(o => o.FinalAmount),
                        lastOrder = g.Max(o => o.OrderDate)
                    })
                    .OrderByDescending(x => x.totalSpent)
                    .Take(limit)
                    .ToList();

                return Ok(topCustomers);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "An error occurred", message = ex.Message });
            }
        }

        [HttpGet("sales-analytics")]
        [Authorize]
        public async Task<ActionResult<object>> GetSalesAnalytics(
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            try
            {
                var query = _context.Orders
                    .Where(o => o.Status != OrderStatus.Cancelled);

                if (startDate.HasValue)
                    query = query.Where(o => o.OrderDate >= startDate.Value);
                if (endDate.HasValue)
                    query = query.Where(o => o.OrderDate <= endDate.Value);

                var orders = await query.ToListAsync();

                var analytics = orders
                    .GroupBy(o => o.OrderDate.Date)
                    .Select(g => new
                    {
                        date = g.Key.ToString("yyyy-MM-dd"),
                        sales = g.Count(),
                        revenue = g.Sum(o => o.FinalAmount),
                        subTotal = g.Sum(o => o.SubTotal),
                        vat = g.Sum(o => o.VatAmount),
                        deliveryFees = g.Sum(o => o.DeliveryFee)
                    })
                    .OrderBy(x => x.date)
                    .ToList();

                return Ok(analytics);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "An error occurred", message = ex.Message });
            }
        }

        [HttpGet("profit-report")]
        [Authorize]
        public async Task<ActionResult<object>> GetProfitReport(
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            try
            {
                var query = _context.Orders
                    .Where(o => o.Status == OrderStatus.Delivered);

                if (startDate.HasValue)
                    query = query.Where(o => o.OrderDate >= startDate.Value);
                if (endDate.HasValue)
                    query = query.Where(o => o.OrderDate <= endDate.Value);

                var orders = await query.ToListAsync();

                var report = new
                {
                    totalRevenue = orders.Sum(o => o.FinalAmount),
                    totalVat = orders.Sum(o => o.VatAmount),
                    totalDeliveryFees = orders.Sum(o => o.DeliveryFee),
                    netProfit = orders.Sum(o => o.FinalAmount - o.VatAmount - o.DeliveryFee),
                    orderCount = orders.Count,
                    averageOrderValue = orders.Any() ? orders.Average(o => o.FinalAmount) : 0
                };

                return Ok(report);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "An error occurred", message = ex.Message });
            }
        }

        [HttpGet]
        [Authorize]
        public async Task<ActionResult<IEnumerable<OrderResponseDto>>> GetUserOrders(
           [FromQuery] int? page = 1,
           [FromQuery] int? pageSize = 10,
           [FromQuery] string? status = null,
           [FromQuery] DateTime? fromDate = null,
           [FromQuery] DateTime? toDate = null)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId == null)
                return Problem(
                    statusCode: 401,
                    title: "غير مصرح",
                    detail: "لم يتم العثور على المستخدم"
                );

            try
            {
                var isAdmin = User.Claims
                    .Any(c => c.Type == ClaimTypes.Role && c.Value.ToLower() == "admin");

                var ordersQuery = _context.Orders
                    .Include(o => o.Items)
                        .ThenInclude(i => i.Product)
                    .Include(o => o.PaymentDetails)
                    .Include(o => o.Address)
                    .Include(o => o.User)
                    .AsQueryable();

                // تعديل هنا: جميع المستخدمين (عاديين أو مسؤولين) سيشاهدون طلباتهم الشخصية فقط
                ordersQuery = ordersQuery.Where(o => o.UserId == userId);

                // تطبيق الفلاتر الأخرى كما هي
                if (!string.IsNullOrWhiteSpace(status))
                {
                    if (Enum.TryParse<OrderStatus>(status, true, out var orderStatus))
                    {
                        ordersQuery = ordersQuery.Where(o => o.Status == orderStatus);
                    }
                }

                if (fromDate.HasValue)
                {
                    ordersQuery = ordersQuery.Where(o => o.OrderDate >= fromDate.Value.Date);
                }

                if (toDate.HasValue)
                {
                    ordersQuery = ordersQuery.Where(o => o.OrderDate <= toDate.Value.Date.AddDays(1));
                }

                var totalOrders = await ordersQuery.CountAsync();
                var validPageSize = Math.Min(Math.Max(1, pageSize ?? 10), 50);
                var validPage = Math.Max(1, page ?? 1);
                var skip = (validPage - 1) * validPageSize;

                ordersQuery = ordersQuery
                    .OrderByDescending(o => o.OrderDate)
                    .Skip(skip)
                    .Take(validPageSize);

                // استخدام نفس تنسيق المخرجات لجميع المستخدمين
                var orders = await ordersQuery
                    .Select(o => new OrderResponseDto
                    {
                        Id = o.Id,
                        OrderDate = o.OrderDate,
                        Status = o.Status.ToString(),
                        SubTotal = o.SubTotal,
                        DiscountAmount = o.DiscountAmount, // إضافة معلومات الخصم
                        VatAmount = o.VatAmount,
                        TotalAmount = o.TotalAmount,
                        DeliveryFee = o.DeliveryFee,
                        FinalAmount = o.FinalAmount,
                        HasDiscount = o.DiscountAmount > 0, // إضافة معلومات وجود خصم
                        PaymentStatus = o.PaymentDetails != null ? o.PaymentDetails.Status.ToString() : null,
                        PaymentMethod = o.PaymentDetails != null ? o.PaymentDetails.PaymentMethod.ToString() : null,
                        PaymentDetails = o.PaymentDetails != null ? new PaymentDetailsDto
                        {
                            PaymentMethod = o.PaymentDetails.PaymentMethod.ToString(),
                            Status = o.PaymentDetails.Status.ToString(),
                            PaidAt = o.PaymentDetails.PaidAt,
                            TransactionId = o.PaymentDetails.TransactionId,
                            ErrorMessage = o.PaymentDetails.ErrorMessage,
                            IsRefunded = o.PaymentDetails.IsRefunded,
                            RefundedAt = o.PaymentDetails.RefundedAt,
                            RefundAmount = o.PaymentDetails.RefundAmount
                        } : null,
                        Items = o.Items
                            .Where(i => i.Product != null)
                            .Select(i => new OrderItemDto
                            {
                                ProductId = i.ProductId,
                                ProductName = i.Product.Name,
                                Quantity = i.Quantity,
                                Price = i.Price,
                                OriginalPrice = i.OriginalPrice, // إضافة السعر الأصلي
                                DiscountAmount = i.DiscountAmount, // إضافة مبلغ الخصم
                                Total = i.Total
                            }).ToList(),
                        DeliveryAddress = new DeliveryAddressDto
                        {
                            FullName = o.Address.FullName,
                            PhoneNumber = o.Address.PhoneNumber,
                            City = o.Address.City,
                            Street = o.Address.Street,
                            BuildingNumber = o.Address.BuildingNumber,
                            AdditionalDetails = o.Address.AdditionalDetails
                        }
                    })
                    .ToListAsync();

                return Ok(new
                {
                    Orders = orders,
                    Pagination = new
                    {
                        CurrentPage = validPage,
                        PageSize = validPageSize,
                        TotalItems = totalOrders,
                        TotalPages = (int)Math.Ceiling(totalOrders / (double)validPageSize)
                    },
                    Filters = new
                    {
                        Status = status,
                        FromDate = fromDate,
                        ToDate = toDate
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving orders for user {UserId}", userId);
                return Problem(
                    statusCode: 500,
                    title: "خطأ في النظام",
                    detail: "حدث خطأ أثناء جلب الطلبات"
                );
            }
        }


        [HttpGet("admin/orders")]
        [Authorize]
        public async Task<ActionResult<IEnumerable<OrderResponseDto>>> GetAdminOrders(
            [FromQuery] int? page = 1,
            [FromQuery] int? pageSize = 10,
            [FromQuery] string? status = null,
            [FromQuery] DateTime? fromDate = null,
            [FromQuery] DateTime? toDate = null)
        {
            try
            {
                // التحقق من المستخدم والصلاحيات
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (userId == null)
                {
                    _logger.LogWarning("Unauthorized access attempt to admin orders - no user ID");
                    return Problem(
                        statusCode: 401,
                        title: "غير مصرح",
                        detail: "لم يتم العثور على المستخدم"
                    );
                }

                var isAdmin = User.IsInRole("Admin") || User.IsInRole("admin");
                _logger.LogInformation($"User {userId} accessing admin orders. IsAdmin: {isAdmin}");

                if (!isAdmin)
                {
                    _logger.LogWarning($"Unauthorized access attempt to admin orders by user {userId}");
                    return Problem(
                        statusCode: 403,
                        title: "غير مصرح",
                        detail: "ليس لديك صلاحية للوصول إلى هذه البيانات"
                    );
                }

                var ordersQuery = _context.Orders
                    .Include(o => o.Items)
                        .ThenInclude(i => i.Product)
                    .Include(o => o.PaymentDetails)
                    .Include(o => o.Address)
                    .Include(o => o.User)
                    .AsQueryable();

                _logger.LogInformation($"Fetching admin orders with filters - Status: {status}, FromDate: {fromDate}, ToDate: {toDate}");

                // تطبيق الفلترة
                if (!string.IsNullOrWhiteSpace(status))
                {
                    if (Enum.TryParse<OrderStatus>(status, true, out var orderStatus))
                    {
                        ordersQuery = ordersQuery.Where(o => o.Status == orderStatus);
                    }
                }

                if (fromDate.HasValue)
                {
                    ordersQuery = ordersQuery.Where(o => o.OrderDate >= fromDate.Value.Date);
                }

                if (toDate.HasValue)
                {
                    ordersQuery = ordersQuery.Where(o => o.OrderDate <= toDate.Value.Date.AddDays(1));
                }

                var totalOrders = await ordersQuery.CountAsync();
                var validPageSize = Math.Min(Math.Max(1, pageSize ?? 10), 50);
                var validPage = Math.Max(1, page ?? 1);
                var skip = (validPage - 1) * validPageSize;

                _logger.LogInformation($"Total orders found: {totalOrders}, Page: {validPage}, PageSize: {validPageSize}");

                ordersQuery = ordersQuery
                    .OrderByDescending(o => o.OrderDate)
                    .Skip(skip)
                    .Take(validPageSize);

                var ordersWithUserInfo = await ordersQuery
                    .Select(o => new
                    {
                        Order = new OrderResponseDto
                        {
                            Id = o.Id,
                            OrderDate = o.OrderDate,
                            Status = o.Status.ToString(),
                            SubTotal = o.SubTotal,
                            DiscountAmount = o.DiscountAmount,
                            VatAmount = o.VatAmount,
                            TotalAmount = o.TotalAmount,
                            DeliveryFee = o.DeliveryFee,
                            FinalAmount = o.FinalAmount,
                            HasDiscount = o.DiscountAmount > 0,
                            PaymentStatus = o.PaymentDetails != null ? o.PaymentDetails.Status.ToString() : null,
                            PaymentMethod = o.PaymentDetails != null ? o.PaymentDetails.PaymentMethod.ToString() : null,
                            PaymentDetails = o.PaymentDetails != null ? new PaymentDetailsDto
                            {
                                PaymentMethod = o.PaymentDetails.PaymentMethod.ToString(),
                                Status = o.PaymentDetails.Status.ToString(),
                                PaidAt = o.PaymentDetails.PaidAt,
                                TransactionId = o.PaymentDetails.TransactionId,
                                ErrorMessage = o.PaymentDetails.ErrorMessage,
                                IsRefunded = o.PaymentDetails.IsRefunded,
                                RefundedAt = o.PaymentDetails.RefundedAt,
                                RefundAmount = o.PaymentDetails.RefundAmount
                            } : null,
                            Items = o.Items
                                .Where(i => i.Product != null)
                                .Select(i => new OrderItemDto
                                {
                                    ProductId = i.ProductId,
                                    ProductName = i.Product.Name,
                                    Quantity = i.Quantity,
                                    Price = i.Price,
                                    OriginalPrice = i.OriginalPrice,
                                    DiscountAmount = i.DiscountAmount,
                                    Total = i.Total
                                }).ToList(),
                            DeliveryAddress = new DeliveryAddressDto
                            {
                                FullName = o.Address.FullName,
                                PhoneNumber = o.Address.PhoneNumber,
                                City = o.Address.City,
                                Street = o.Address.Street,
                                BuildingNumber = o.Address.BuildingNumber,
                                AdditionalDetails = o.Address.AdditionalDetails
                            }
                        },
                        UserInfo = new
                        {
                            UserId = o.UserId,
                            UserName = o.User.Name,
                            UserEmail = o.User.Email,
                            UserPhone = o.User.PhoneNumber
                        }
                    })
                    .ToListAsync();

                _logger.LogInformation($"Successfully retrieved {ordersWithUserInfo.Count} orders for admin view");

                return Ok(new
                {
                    Orders = ordersWithUserInfo,
                    Pagination = new
                    {
                        CurrentPage = validPage,
                        PageSize = validPageSize,
                        TotalItems = totalOrders,
                        TotalPages = (int)Math.Ceiling(totalOrders / (double)validPageSize)
                    },
                    Filters = new
                    {
                        Status = status,
                        FromDate = fromDate,
                        ToDate = toDate
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving orders for admin");
                return Problem(
                    statusCode: 500,
                    title: "خطأ في النظام",
                    detail: "حدث خطأ أثناء جلب الطلبات"
                );
            }
        }

        private async Task<PaymentResponse> ProcessPaymentAsync(PaymentDetails paymentDetails)
        {
            try
            {
                _logger.LogInformation($"Processing payment with method: {paymentDetails.PaymentMethod}");

                if (paymentDetails.PaymentMethod == PaymentMethodType.CREDIT_CARD ||
                    paymentDetails.PaymentMethod == PaymentMethodType.MADA)
                {
                    // تأكد من وجود بيانات البطاقة
                    if (paymentDetails.PaymentData == null ||
                        !paymentDetails.PaymentData.ContainsKey("cardNumber") ||
                        !paymentDetails.PaymentData.ContainsKey("expiryDate") ||
                        !paymentDetails.PaymentData.ContainsKey("cvv"))
                    {
                        _logger.LogWarning("Missing credit card details");
                        return new PaymentResponse
                        {
                            IsSuccess = false,
                            ErrorMessage = "بيانات البطاقة غير مكتملة",
                            TransactionId = null
                        };
                    }

                    // استخدم خدمة الدفع
                    var paymentResult = await _paymentService.ProcessCreditCardPaymentAsync(paymentDetails);

                    // إذا نجحت عملية الدفع، قم بتحديث حالة الدفع إلى "مكتمل"
                    if (paymentResult.IsSuccess)
                    {
                        paymentDetails.Status = PaymentStatus.Completed;
                        paymentDetails.PaidAt = DateTime.UtcNow;
                        paymentDetails.UpdatedAt = DateTime.UtcNow;
                        paymentDetails.TransactionId = paymentResult.TransactionId;
                    }

                    return paymentResult;
                }
                else if (paymentDetails.PaymentMethod == PaymentMethodType.CASH_ON_DELIVERY)
                {
                    _logger.LogInformation("Processing cash on delivery payment");
                    paymentDetails.Status = PaymentStatus.Processing;
                    paymentDetails.TransactionId = $"COD-{DateTime.UtcNow.Ticks}";
                    paymentDetails.PaidAt = DateTime.UtcNow;
                    paymentDetails.UpdatedAt = DateTime.UtcNow;

                    return new PaymentResponse
                    {
                        IsSuccess = true,
                        TransactionId = paymentDetails.TransactionId,
                        ErrorMessage = null
                    };
                }
                else
                {
                    _logger.LogError($"Payment method not supported: {paymentDetails.PaymentMethod}");
                    return new PaymentResponse
                    {
                        IsSuccess = false,
                        ErrorMessage = "عذراً، حالياً نقبل فقط الدفع عند الاستلام أو البطاقات الائتمانية",
                        TransactionId = null
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Payment processing failed");
                return new PaymentResponse
                {
                    IsSuccess = false,
                    ErrorMessage = "حدث خطأ أثناء معالجة الدفع",
                    TransactionId = null
                };
            }
        }

        [HttpGet("admin/orders/{id}")]
        [Authorize(Roles = "admin")]
        public async Task<ActionResult<OrderResponseDto>> GetAdminOrderDetails(int id)
        {
            // نفس المنطق مع التحقق الإضافي من دور المسؤول
            var isAdmin = User.IsInRole("admin");
            if (!isAdmin)
            {
                return Problem(
                    statusCode: 403,
                    title: "غير مصرح",
                    detail: "ليس لديك صلاحية للوصول إلى هذه البيانات"
                );
            }

            var order = await _context.Orders
                .Include(o => o.Items)
                    .ThenInclude(i => i.Product)
                .Include(o => o.PaymentDetails)
                .Include(o => o.Address)
                .Include(o => o.User)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null)
                return NotFound();

            // إرجاع البيانات مع معلومات المستخدم للأدمن
            return Ok(new
            {
                Order = MapToOrderResponseDto(order),
                UserInfo = new
                {
                    UserId = order.UserId,
                    UserName = order.User.Name,
                    UserEmail = order.User.Email,
                    UserPhone = order.User.PhoneNumber
                }
            });
        }

        [HttpGet("{id}")]
        [Authorize]
        public async Task<ActionResult<OrderResponseDto>> GetOrder(int id)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId == null)
                return Problem(
                    statusCode: 401,
                    title: "غير مصرح",
                    detail: "لم يتم العثور على المستخدم"
                );

            try
            {
                var order = await _context.Orders
                    .Include(o => o.Items)
                        .ThenInclude(i => i.Product)
                    .Include(o => o.PaymentDetails)
                    .Include(o => o.Address)
                    .FirstOrDefaultAsync(o => o.Id == id);

                if (order == null)
                    return Problem(
                        statusCode: 404,
                        title: "غير موجود",
                        detail: "لم يتم العثور على الطلب"
                    );

                if (order.UserId != userId && !User.IsInRole("Admin"))
                {
                    return Problem(
                        statusCode: 403,
                        title: "غير مصرح",
                        detail: "ليس لديك صلاحية للوصول إلى هذا الطلب"
                    );
                }

                return Ok(MapToOrderResponseDto(order));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving order {OrderId} for user {UserId}", id, userId);
                return Problem(
                    statusCode: 500,
                    title: "خطأ في النظام",
                    detail: "حدث خطأ أثناء جلب تفاصيل الطلب"
                );
            }
        }

        private async Task GenerateInvoiceForOrder(Order order)
        {
            try
            {
                string htmlContent = GenerateInvoiceHtml(order);

                var globalSettings = new GlobalSettings
                {
                    ColorMode = ColorMode.Color,
                    Orientation = Orientation.Portrait,
                    PaperSize = PaperKind.A4,
                    Margins = new MarginSettings { Top = 10, Bottom = 10, Left = 10, Right = 10 },
                    DocumentTitle = $"Invoice-{order.Id}"
                };

                var objectSettings = new ObjectSettings
                {
                    PagesCount = true,
                    HtmlContent = htmlContent,
                    WebSettings = { DefaultEncoding = "utf-8" },
                    HeaderSettings = new HeaderSettings
                    {
                        FontName = "Arial",
                        FontSize = 9,
                        Right = "Page [page] of [toPage]",
                        Left = $"رقم الطلب: {order.Id}",
                        Line = true
                    },
                    FooterSettings = new FooterSettings
                    {
                        FontName = "Arial",
                        FontSize = 9,
                        Line = true,
                        Center = $"© {DateTime.Now.Year} Sadiq Aldubaisi  متجرنا"
                    }
                };

                var pdf = new HtmlToPdfDocument()
                {
                    GlobalSettings = globalSettings,
                    Objects = { objectSettings }
                };

                byte[] pdfBytes = _pdfConverter.Convert(pdf);

                string invoiceDirectory = Path.Combine(_environment.WebRootPath, "invoices");
                Directory.CreateDirectory(invoiceDirectory);

                string invoiceFileName = $"Invoice-{order.Id}-{DateTime.Now:yyyyMMddHHmmss}.pdf";
                string invoicePath = Path.Combine(invoiceDirectory, invoiceFileName);

                await System.IO.File.WriteAllBytesAsync(invoicePath, pdfBytes);

                order.InvoicePath = Path.Combine("invoices", invoiceFileName).Replace("\\", "/");
                _context.Orders.Update(order);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error generating invoice for order {order.Id}: {ex.Message}");
                throw;
            }
        }

        [HttpGet("{id}/invoice")]
        [Authorize]
        public async Task<IActionResult> GetInvoice(int id)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId == null)
                return Problem(
                    statusCode: 401,
                    title: "غير مصرح",
                    detail: "لم يتم العثور على المستخدم"
                );

            try
            {
                var isAdmin = User.Claims
                    .Any(c => c.Type == ClaimTypes.Role && c.Value.ToLower() == "admin");

                var orderQuery = _context.Orders
                    .Include(o => o.Items)
                        .ThenInclude(i => i.Product)
                    .Include(o => o.PaymentDetails)
                    .Include(o => o.Address);

                // إذا كان المستخدم ليس أدمن، نقيد البحث بالمستخدم الحالي فقط
                var order = await (isAdmin
                    ? orderQuery.FirstOrDefaultAsync(o => o.Id == id)
                    : orderQuery.FirstOrDefaultAsync(o => o.Id == id && o.UserId == userId));

                if (order == null)
                    return Problem(
                        statusCode: 404,
                        title: "غير موجود",
                        detail: "لم يتم العثور على الطلب"
                    );

                if (string.IsNullOrEmpty(order.InvoicePath))
                {
                    try
                    {
                        await GenerateInvoiceForOrder(order);
                    }
                    catch (Exception ex)
                    {
                        return Problem(
                            statusCode: 500,
                            title: "خطأ في النظام",
                            detail: "فشل في إنشاء الفاتورة"
                        );
                    }
                }

                var filePath = Path.Combine(_environment.WebRootPath, order.InvoicePath.TrimStart('/'));
                if (!System.IO.File.Exists(filePath))
                {
                    try
                    {
                        await GenerateInvoiceForOrder(order);
                        filePath = Path.Combine(_environment.WebRootPath, order.InvoicePath.TrimStart('/'));
                    }
                    catch (Exception ex)
                    {
                        return Problem(
                            statusCode: 500,
                            title: "خطأ في النظام",
                            detail: "فشل في إعادة إنشاء الفاتورة"
                        );
                    }
                }

                var fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);
                return File(fileBytes, "application/pdf", Path.GetFileName(filePath));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating/retrieving invoice for order {OrderId}", id);
                return Problem(
                    statusCode: 500,
                    title: "خطأ في النظام",
                    detail: "حدث خطأ أثناء جلب الفاتورة"
                );
            }
        }

        private string GenerateInvoiceHtml(Order order)
        {
            var sb = new StringBuilder();
            var paymentMethodText = order.PaymentDetails?.PaymentMethod.ToString() switch
            {
                "CASH_ON_DELIVERY" => "الدفع عند الاستلام",
                "CREDIT_CARD" => "بطاقة ائتمان",
                "BANK_TRANSFER" => "تحويل بنكي",
                "MADA" => "مدى",
                _ => "غير محدد"
            };

            var orderStatusText = order.Status switch
            {
                OrderStatus.Pending => "قيد المراجعة",
                OrderStatus.Processing => "جاري التجهيز",
                OrderStatus.Shipped => "تم الشحن",
                OrderStatus.Delivered => "تم التوصيل",
                OrderStatus.Cancelled => "تم الإلغاء",
                _ => order.Status.ToString()
            };

            var paymentStatusText = order.PaymentDetails?.Status.ToString() switch
            {
                "Pending" => "قيد الانتظار",
                "Processing" => "قيد المعالجة",
                "Completed" => "مكتمل",
                "Failed" => "فشل الدفع",
                "Refunded" => "تم الاسترجاع",
                _ => order.PaymentDetails?.Status.ToString() ?? "غير معروف"
            };


            sb.Append(@"
    <!DOCTYPE html>
    <html lang='ar' dir='rtl'>
    <head>
        <meta charset='UTF-8'>
        <title>فاتورة الطلب</title>
        <style>
            body { 
                font-family: 'Arial', sans-serif; 
                direction: rtl; 
                text-align: right;
                margin: 0;
                padding: 0;
                background-color: #f8f9fa;
                color: #333;
            }
            .invoice-container { 
                width: 100%; 
                max-width: 800px; 
                margin: 0 auto; 
                padding: 20px;
                background-color: white;
            }
            .invoice-number {
                float: right;
                margin-top: 10px;
                color: #666;
            }
            .page-number {
                float: left;
                margin-top: 10px;
                color: #666;
            }
            .invoice-header { 
                text-align: center;
                margin: 40px 0;
                clear: both;
            }
            .invoice-header h1 {
                margin-bottom: 10px;
                color: #333;
                font-size: 24px;
            }
            .details-section {
                display: flex;
                justify-content: space-between;
                margin-bottom: 30px;
                gap: 20px;
            }
            .details-box {
                flex: 1;
                background-color: #f8f9fa;
                padding: 20px;
                border-radius: 4px;
            }
            .details-box h3 {
                margin-top: 0;
                margin-bottom: 15px;
                padding-bottom: 10px;
                border-bottom: 1px solid #ddd;
                color: #333;
            }
            .details-row {
                display: flex;
                justify-content: space-between;
                margin-bottom: 8px;
                line-height: 1.5;
            }
            .details-label {
                color: #666;
                flex: 1;
            }
            .details-value {
                flex: 1;
                text-align: right;
                font-weight: 500;
            }
            .products-table { 
                width: 100%; 
                border-collapse: collapse;
                margin: 20px 0;
                background-color: white;
            }
            .products-table th { 
                background-color: #333;
                color: white;
                padding: 12px;
                font-weight: normal;
            }
            .products-table td { 
                padding: 12px;
                border-bottom: 1px solid #ddd;
            }
            .summary-section {
                background-color: #f8f9fa;
                padding: 20px;
                border-radius: 4px;
                margin-top: 30px;
            }
            .summary-row {
                display: flex;
                justify-content: space-between;
                margin-bottom: 8px;
            }
            .final-total {
                font-weight: bold;
                font-size: 1.2em;
                margin-top: 15px;
                padding-top: 15px;
                border-top: 2px solid #ddd;
            }
            .discount-row {
                color: #e74c3c;
                font-weight: bold;
            }
            .footer-section {
                margin-top: 20px;
                text-align: right;
            }
            .contact-info {
                margin-bottom: 10px;
                color: #666;
            }
            .contact-link {
                color: #0066cc;
                text-decoration: none;
            }
            .footer-text {
                text-align: center;
                color: #666;
                margin-top: 20px;
                padding-top: 10px;
                border-top: 1px solid #ddd;
            }
            .copyright {
                text-align: center;
                margin-top: 10px;
                color: #888;
                font-size: 0.9em;
            }
        </style>
    </head>
    <body>


        <div class='invoice-container'>
            <div class='invoice-number'>رقم الطلب: " + order.Id + @"</div>
            <div class='page-number'>Page 1 of 1</div>
<div class='invoice-header'>
    <h1>فاتورة رقم " + order.Id + @"</h1>
    <p>تاريخ الطلب: " + order.OrderDate.ToString("yyyy/MM/dd - HH:mm") + @"</p>
    <p>حالة الطلب: " + orderStatusText + @"</p>
</div>

            <div class='details-section'>
                <div class='details-box'>
                    <h3>تفاصيل العميل</h3>
                    <div class='details-row'>
                        <span class='details-label'>الاسم:</span>
                        <span class='details-value'>" + order.Address.FullName + @"</span>
                    </div>
                    <div class='details-row'>
                        <span class='details-label'>رقم الهاتف:</span>
                        <span class='details-value'>" + order.Address.PhoneNumber + @"</span>
                    </div>
                    <div class='details-row'>
                        <span class='details-label'>العنوان:</span>
                        <span class='details-value'>" + $"{order.Address.City} {order.Address.BuildingNumber}" + @"</span>
                    </div>
                </div>

                <div class='details-box'>
                    <h3>تفاصيل الدفع</h3>
                    <div class='details-row'>
                        <span class='details-label'>طريقة الدفع:</span>
                        <span class='details-value'>" + paymentMethodText + @"</span>
                    </div>
                    <div class='details-row'>
                        <span class='details-label'>حالة الدفع:</span>
                        <span class='details-value'>" + paymentStatusText + @"</span>
                    </div>
                    <div class='details-row'>
                        <span class='details-label'>رقم العملية:</span>
                        <span class='details-value'>" + order.PaymentDetails?.TransactionId + @"</span>
                    </div>
                    <div class='details-row'>
                        <span class='details-label'>تاريخ الدفع:</span>
                        <span class='details-value'>" + order.PaymentDetails?.PaidAt?.ToString("yyyy/MM/dd") + @"</span>
                    </div>
                </div>
            </div>

            <h3>تفاصيل المنتجات</h3>
            <table class='products-table'>
                <thead>
                    <tr>
                        <th>المنتج</th>
                        <th>الكمية</th>
                        <th>السعر الأصلي</th>
                        <th>السعر بعد الخصم</th>
                        <th>الخصم</th>
                        <th>الإجمالي</th>
                    </tr>
                </thead>
                <tbody>");

            foreach (var item in order.Items)
            {
                bool hasDiscount = item.DiscountAmount > 0;
                decimal originalPrice = hasDiscount ? item.OriginalPrice : item.Price;
                decimal discountAmount = hasDiscount ? item.DiscountAmount : 0;
                decimal finalPrice = item.Price;
                decimal rowTotal = item.Total;

                sb.Append($@"
                    <tr>
                        <td>{item.Product?.Name ?? "منتج"}</td>
                        <td>{item.Quantity}</td>
                        <td>{originalPrice:N2} ريال</td>
                        <td>{finalPrice:N2} ريال</td>
                        <td>{(hasDiscount ? discountAmount.ToString("N2") + " ريال" : "-")}</td>
                        <td>{rowTotal:N2} ريال</td>
                    </tr>");
            }

            sb.Append($@"
                </tbody>
            </table>

            <div class='summary-section'>
                <div class='summary-row'>
                    <span>المجموع الفرعي:</span>
                    <span>{order.SubTotal:N2} ريال</span>
                </div>");

            if (order.DiscountAmount > 0)
            {
                sb.Append($@"
                <div class='summary-row discount-row'>
                    <span>الخصم:</span>
                    <span>- {order.DiscountAmount:N2} ريال</span>
                </div>");
            }

            sb.Append($@"
                <div class='summary-row'>
                    <span>ضريبة القيمة المضافة (15%):</span>
                    <span>{order.VatAmount:N2} ريال</span>
                </div>
                <div class='summary-row'>
                    <span>رسوم التوصيل:</span>
                    <span>{order.DeliveryFee:N2} ريال</span>
                </div>
                <div class='summary-row final-total'>
                    <span>الإجمالي النهائي:</span>
                    <span>{order.FinalAmount:N2} ريال</span>
                </div>
            </div>

            <div class='footer-section'>
                <div class='contact-info'>
                    للاستفسارات، يمكنكم التواصل معنا عبر: 
                    <a href='mailto:sadiqhd@gmail.com' class='contact-link'>sadiqhd@gmail.com</a> | 
                    <a href='tel:0553065029' class='contact-link'>0553065029</a>
                </div>
                <div class='footer-text'>شكراً لتسوقك معنا!</div>
            </div>
        </div>
    </body>
    </html>");

            return sb.ToString();
        }

        [HttpPost("regenerate-all-invoices")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> RegenerateAllInvoices()
        {
            try
            {
                var allOrders = await _context.Orders
                    .Include(o => o.Items)
                        .ThenInclude(i => i.Product)
                    .Include(o => o.PaymentDetails)
                    .Include(o => o.Address)
                    .ToListAsync();

                int successCount = 0;
                var errors = new List<string>();

                foreach (var order in allOrders)
                {
                    try
                    {
                        // احذف الملف القديم إذا وجد
                        if (!string.IsNullOrEmpty(order.InvoicePath))
                        {
                            var oldFilePath = Path.Combine(_environment.WebRootPath, order.InvoicePath.TrimStart('/'));
                            if (System.IO.File.Exists(oldFilePath))
                            {
                                System.IO.File.Delete(oldFilePath);
                            }
                        }

                        // إنشاء فاتورة جديدة
                        await GenerateInvoiceForOrder(order);
                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Error regenerating invoice for order {order.Id}: {ex.Message}");
                        errors.Add($"Order {order.Id}: {ex.Message}");
                    }
                }

                return Ok(new
                {
                    Message = "تم إعادة إنشاء الفواتير",
                    TotalOrders = allOrders.Count,
                    SuccessfullyGenerated = successCount,
                    Failed = allOrders.Count - successCount,
                    Errors = errors
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing invoices regeneration");
                return Problem(
                    statusCode: 500,
                    title: "خطأ في النظام",
                    detail: "حدث خطأ أثناء إعادة إنشاء الفواتير"
                );
            }
        }

        [HttpPut("admin/{id}/status")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<OrderResponseDto>> UpdateOrderStatus(int id, [FromBody] UpdateOrderStatusDto request)
        {
            var strategy = _context.Database.CreateExecutionStrategy();

            return await strategy.ExecuteAsync(async () => {
                // استخدام معاملة قاعدة بيانات لضمان اتساق البيانات
                using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    if (!Enum.TryParse<OrderStatus>(request.Status, true, out OrderStatus newStatus))
                    {
                        return Problem(
                            statusCode: 400,
                            title: "قيمة غير صالحة",
                            detail: "حالة الطلب غير صالحة"
                        );
                    }

                    _logger.LogInformation($"Attempting to update order {id} status to {newStatus}");

                    // استخدام AsTracking() لضمان تتبع التغييرات
                    var order = await _context.Orders
                        .AsTracking()
                        .Include(o => o.Items)
                            .ThenInclude(i => i.Product)
                        .Include(o => o.PaymentDetails)
                        .Include(o => o.Address)
                        .FirstOrDefaultAsync(o => o.Id == id);

                    if (order == null)
                    {
                        _logger.LogWarning($"Order {id} not found for status update");
                        return Problem(
                            statusCode: 404,
                            title: "غير موجود",
                            detail: "لم يتم العثور على الطلب"
                        );
                    }

                    _logger.LogInformation($"Current status of order {id}: {order.Status}, changing to: {newStatus}");

                    if (!IsValidStatusTransition(order.Status, newStatus))
                    {
                        _logger.LogWarning($"Invalid status transition for order {id} from {order.Status} to {newStatus}");
                        return Problem(
                            statusCode: 400,
                            title: "حالة غير صالحة",
                            detail: "لا يمكن تغيير حالة الطلب إلى الحالة المطلوبة"
                        );
                    }

                    // حفظ الحالة القديمة
                    var oldStatus = order.Status;

                    // تحديث حالة الطلب
                    order.Status = newStatus;

                    // تحديث حالة الدفع بناء على حالة الطلب
                    if (order.PaymentDetails != null)
                    {
                        if (newStatus == OrderStatus.Delivered)
                        {
                            order.PaymentDetails.Status = PaymentStatus.Completed;
                            order.PaymentDetails.PaidAt = DateTime.UtcNow;
                        }
                        else if (newStatus == OrderStatus.Cancelled)
                        {
                            order.PaymentDetails.Status = PaymentStatus.Refunded;
                            order.PaymentDetails.RefundedAt = DateTime.UtcNow;
                            order.PaymentDetails.IsRefunded = true;
                            order.PaymentDetails.RefundAmount = order.TotalAmount;
                        }

                        order.PaymentDetails.UpdatedAt = DateTime.UtcNow;
                    }

                    // حفظ التغييرات في قاعدة البيانات
                    await _context.SaveChangesAsync();

                    // إعادة تحميل الكائن للتأكد من تحديث البيانات
                    await _context.Entry(order).ReloadAsync();

                    if (order.Status != newStatus)
                    {
                        _logger.LogError($"Failed to update order status. Expected: {newStatus}, Actual: {order.Status}");
                        await transaction.RollbackAsync();
                        return Problem(
                            statusCode: 500,
                            title: "خطأ في التحديث",
                            detail: "فشل تحديث حالة الطلب في قاعدة البيانات"
                        );
                    }

                    // إنشاء فاتورة جديدة بناء على البيانات المحدثة
                    try
                    {
                        // حذف الفاتورة القديمة (إذا وجدت)
                        if (!string.IsNullOrEmpty(order.InvoicePath))
                        {
                            var oldFilePath = Path.Combine(_environment.WebRootPath, order.InvoicePath.TrimStart('/'));
                            if (System.IO.File.Exists(oldFilePath))
                            {
                                System.IO.File.Delete(oldFilePath);
                                _logger.LogInformation($"Deleted old invoice for order {order.Id}");
                            }
                        }

                        // إنشاء فاتورة جديدة
                        await GenerateInvoiceForOrder(order);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Error regenerating invoice after status update for order {id}: {ex.Message}");
                        _logger.LogError($"Stack trace: {ex.StackTrace}");
                        // نستمر في التنفيذ حتى لو فشل إنشاء الفاتورة
                    }

                    // إتمام المعاملة
                    await transaction.CommitAsync();

                    _logger.LogInformation($"Order {id} status successfully updated from {oldStatus} to {newStatus}");

                    return Ok(MapToOrderResponseDto(order));
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Error updating order status for order {OrderId}", id);
                    return Problem(
                        statusCode: 500,
                        title: "خطأ في النظام",
                        detail: "حدث خطأ أثناء تحديث حالة الطلب"
                    );
                }
            });
        }

        [HttpPut("admin/{id}/payment-status")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<OrderResponseDto>> UpdatePaymentStatus(int id, [FromBody] UpdatePaymentStatusDto request)
        {
            try
            {
                if (!Enum.TryParse<PaymentStatus>(request.Status, true, out PaymentStatus newStatus))
                {
                    return Problem(
                        statusCode: 400,
                        title: "قيمة غير صالحة",
                        detail: "حالة الدفع غير صالحة"
                    );
                }

                var order = await _context.Orders
                    .Include(o => o.Items)
                        .ThenInclude(i => i.Product)
                    .Include(o => o.PaymentDetails)
                    .Include(o => o.Address)
                    .FirstOrDefaultAsync(o => o.Id == id);

                if (order == null)
                {
                    return Problem(
                        statusCode: 404,
                        title: "غير موجود",
                        detail: "لم يتم العثور على الطلب"
                    );
                }

                if (order.PaymentDetails == null)
                {
                    return Problem(
                        statusCode: 400,
                        title: "خطأ في البيانات",
                        detail: "لا توجد تفاصيل دفع للطلب"
                    );
                }

                if (!IsValidPaymentStatusTransition(order.PaymentDetails.Status, newStatus))
                {
                    return Problem(
                        statusCode: 400,
                        title: "حالة غير صالحة",
                        detail: "لا يمكن تغيير حالة الدفع إلى الحالة المطلوبة"
                    );
                }

                order.PaymentDetails.Status = newStatus;
                order.PaymentDetails.UpdatedAt = DateTime.UtcNow;

                if (newStatus == PaymentStatus.Completed)
                {
                    order.PaymentDetails.PaidAt = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();

                try
                {
                    // حذف الفاتورة القديمة إذا وجدت
                    if (!string.IsNullOrEmpty(order.InvoicePath))
                    {
                        var oldFilePath = Path.Combine(_environment.WebRootPath, order.InvoicePath.TrimStart('/'));
                        if (System.IO.File.Exists(oldFilePath))
                        {
                            System.IO.File.Delete(oldFilePath);
                        }
                    }

                    // إنشاء فاتورة جديدة
                    await GenerateInvoiceForOrder(order);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error regenerating invoice after payment status update for order {id}: {ex.Message}");
                    // نستمر في التنفيذ حتى لو فشل إنشاء الفاتورة
                }

                return Ok(MapToOrderResponseDto(order));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating payment status for order {OrderId}", id);
                return Problem(
                    statusCode: 500,
                    title: "خطأ في النظام",
                    detail: "حدث خطأ أثناء تحديث حالة الدفع"
                );
            }
        }

        // Validation Methods
        private bool IsValidStatusTransition(OrderStatus currentStatus, OrderStatus newStatus)
        {
            switch (currentStatus)
            {
                case OrderStatus.Pending:
                    return newStatus == OrderStatus.Processing
                        || newStatus == OrderStatus.Cancelled;

                case OrderStatus.Processing:
                    return newStatus == OrderStatus.Shipped
                        || newStatus == OrderStatus.Cancelled;

                case OrderStatus.Shipped:
                    return newStatus == OrderStatus.Delivered
                        || newStatus == OrderStatus.Cancelled;

                case OrderStatus.Delivered:
                    return false; // Final state, no transitions allowed

                case OrderStatus.Cancelled:
                    return false; // Final state, no transitions allowed

                default:
                    return false;
            }
        }

        private bool IsValidPaymentStatusTransition(PaymentStatus currentStatus, PaymentStatus newStatus)
        {
            switch (currentStatus)
            {
                case PaymentStatus.Pending:
                    return newStatus == PaymentStatus.Processing
                        || newStatus == PaymentStatus.Failed;

                case PaymentStatus.Processing:
                    return newStatus == PaymentStatus.Completed
                        || newStatus == PaymentStatus.Failed;

                case PaymentStatus.Completed:
                    return newStatus == PaymentStatus.Refunded;

                case PaymentStatus.Failed:
                    return newStatus == PaymentStatus.Processing;

                case PaymentStatus.Refunded:
                    return false; // Final state, no transitions allowed

                default:
                    return false;
            }
        }

        // تعديل الدالة MapToOrderResponseDto
        private OrderResponseDto MapToOrderResponseDto(Order order)
        {
            return new OrderResponseDto
            {
                Id = order.Id,
                OrderDate = order.OrderDate,
                Status = order.Status.ToString(),
                SubTotal = order.SubTotal,
                DiscountAmount = order.DiscountAmount,
                VatAmount = order.VatAmount,
                TotalAmount = order.TotalAmount,
                DeliveryFee = order.DeliveryFee,
                FinalAmount = order.FinalAmount,
                HasDiscount = order.DiscountAmount > 0,
                PaymentStatus = order.PaymentDetails?.Status.ToString(),
                PaymentMethod = order.PaymentDetails?.PaymentMethod.ToString(),
                PaymentDetails = order.PaymentDetails != null ? new PaymentDetailsDto
                {
                    PaymentMethod = order.PaymentDetails.PaymentMethod.ToString(),
                    Status = order.PaymentDetails.Status.ToString(),
                    PaidAt = order.PaymentDetails.PaidAt,
                    TransactionId = order.PaymentDetails.TransactionId,
                    ErrorMessage = order.PaymentDetails.ErrorMessage,
                    IsRefunded = order.PaymentDetails.IsRefunded,
                    RefundedAt = order.PaymentDetails.RefundedAt,
                    RefundAmount = order.PaymentDetails.RefundAmount
                } : null,
                // هنا التعديل الرئيسي لضمان التوافق مع الطلبات القديمة
                Items = order.Items.Select(i => new OrderItemDto
                {
                    ProductId = i.ProductId,
                    // استخدم Product.Name كبديل إذا كان ProductName غير موجود
                    ProductName = i.Product?.Name ?? "",
                    Quantity = i.Quantity,
                    Price = i.Price,
                    // استخدم قيم افتراضية للخصائص الجديدة
                    OriginalPrice = i.OriginalPrice > 0 ? i.OriginalPrice : i.Price,
                    DiscountAmount = i.DiscountAmount,
                    Total = i.Total
                }).ToList(),
                DeliveryAddress = new DeliveryAddressDto
                {
                    FullName = order.Address?.FullName ?? "",
                    PhoneNumber = order.Address?.PhoneNumber ?? "",
                    City = order.Address?.City ?? "",
                    Street = order.Address?.Street,
                    BuildingNumber = order.Address?.BuildingNumber,
                    AdditionalDetails = order.Address?.AdditionalDetails
                }
            };
        }

        // DTOs
        public class UpdateOrderStatusDto
        {
            public string Status { get; set; } = string.Empty;
        }

        public class UpdatePaymentStatusDto
        {
            public string Status { get; set; } = string.Empty;
        }

        [HttpPost("{id}/cancel")]
        [Authorize]
        public async Task<ActionResult<OrderResponseDto>> CancelOrder(int id)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId == null)
                return Problem(
                    statusCode: 401,
                    title: "غير مصرح",
                    detail: "لم يتم العثور على المستخدم"
                );

            try
            {
                var order = await _context.Orders
                    .Include(o => o.Items)
                        .ThenInclude(i => i.Product)
                    .Include(o => o.PaymentDetails)
                    .Include(o => o.Address)
                    .FirstOrDefaultAsync(o => o.Id == id && o.UserId == userId);

                if (order == null)
                    return Problem(
                        statusCode: 404,
                        title: "غير موجود",
                        detail: "لم يتم العثور على الطلب"
                    );

                // التحقق من إمكانية إلغاء الطلب
                if (order.Status == OrderStatus.Shipped || order.Status == OrderStatus.Delivered || order.Status == OrderStatus.Cancelled)
                {
                    return Problem(
                        statusCode: 403,
                        title: "غير مسموح",
                        detail: "لا يمكن إلغاء الطلب في حالته الحالية"
                    );
                }

                // التحقق من الوقت (يمكن إلغاء الطلب خلال ساعة من إنشائه فقط)
                var hoursSinceCreation = (DateTime.UtcNow - order.OrderDate).TotalHours;
                if (hoursSinceCreation > 1)
                {
                    return Problem(
                        statusCode: 403,
                        title: "غير مسموح",
                        detail: "لا يمكن إلغاء الطلب بعد مرور ساعة من إنشائه"
                    );
                }

                var strategy = _context.Database.CreateExecutionStrategy();

                return await strategy.ExecuteAsync(async () =>
                {
                    using var transaction = await _context.Database.BeginTransactionAsync();
                    try
                    {
                        // تحديث حالة الطلب
                        order.Status = OrderStatus.Cancelled;

                        // إعادة المنتجات للمخزون
                        foreach (var item in order.Items)
                        {
                            if (item.Product != null)
                            {
                                item.Product.Stock += item.Quantity;
                                _context.Entry(item.Product).State = EntityState.Modified;
                            }
                        }

                        // تحديث حالة الدفع إذا كان موجوداً
                        if (order.PaymentDetails != null)
                        {
                            order.PaymentDetails.Status = PaymentStatus.Refunded;
                            order.PaymentDetails.UpdatedAt = DateTime.UtcNow;
                            order.PaymentDetails.RefundedAt = DateTime.UtcNow;
                            order.PaymentDetails.IsRefunded = true;
                            order.PaymentDetails.RefundAmount = order.TotalAmount;
                        }

                        await _context.SaveChangesAsync();
                        await transaction.CommitAsync();

                        _logger.LogInformation($"Order {order.Id} cancelled successfully by user {userId}");

                        // إعداد DTO للاستجابة
                        return Ok(MapToOrderResponseDto(order));
                    }
                    catch (Exception ex)
                    {
                        await transaction.RollbackAsync();
                        _logger.LogError(ex, "Error cancelling order {OrderId} for user {UserId}", id, userId);
                        return Problem(
                            statusCode: 500,
                            title: "خطأ في النظام",
                            detail: "حدث خطأ أثناء إلغاء الطلب"
                        );
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing order cancellation for order {OrderId}", id);
                return Problem(
                    statusCode: 500,
                    title: "خطأ في النظام",
                    detail: "حدث خطأ أثناء إلغاء الطلب"
                );
            }
        }

        // طرق مساعدة لحساب الخصومات
        private Discount GetBestDiscount(List<Discount> discounts, decimal productPrice)
        {
            return discounts.OrderByDescending(d =>
                d.Type == DiscountType.Percentage ?
                productPrice * d.Value / 100 : d.Value
            ).First();
        }

        private decimal CalculateDiscountedPrice(decimal originalPrice, Discount discount)
        {
            if (discount.Type == DiscountType.Percentage)
            {
                return Math.Round(originalPrice * (1 - discount.Value / 100), 2);
            }
            return Math.Max(0, Math.Round(originalPrice - discount.Value, 2));
        }

        public class PaymentResponse
        {
            public bool IsSuccess { get; set; }
            public string? ErrorMessage { get; set; }
            public string? TransactionId { get; set; }
        }
    }
}