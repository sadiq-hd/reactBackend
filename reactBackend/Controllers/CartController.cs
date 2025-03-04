using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using reactBackend.Data;
using reactBackend.Dtos;
using reactBackend.Models;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;


[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CartController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<CartController> _logger;

    public CartController(ApplicationDbContext context, ILogger<CartController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpPost("add")]
    public async Task<ActionResult> AddToCart([FromBody] AddToCartDto dto)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == dto.ProductId);
        if (product == null)
        {
            return NotFound("المنتج غير موجود.");
        }

        if (product.Stock < dto.Quantity)
        {
            return BadRequest($"الكمية المطلوبة ({dto.Quantity}) غير متوفرة. الكمية المتاحة: {product.Stock}.");
        }

        var cartItem = await _context.CartItems
            .FirstOrDefaultAsync(c => c.UserId == userId && c.ProductId == dto.ProductId);

        if (cartItem == null)
        {
            cartItem = new CartItem
            {
                UserId = userId,
                ProductId = dto.ProductId,
                Quantity = dto.Quantity
            };
            _context.CartItems.Add(cartItem);
        }
        else
        {
            if (cartItem.Quantity + dto.Quantity > product.Stock)
            {
                return BadRequest($"لا يمكن إضافة الكمية المطلوبة. الكمية الإجمالية المطلوبة ({cartItem.Quantity + dto.Quantity}) تتجاوز الكمية المتوفرة ({product.Stock}).");
            }

            cartItem.Quantity += dto.Quantity;
        }

        await _context.SaveChangesAsync();
        return Ok("تمت إضافة المنتج إلى السلة بنجاح.");
    }

    [HttpDelete("remove/{productId}/{quantityToRemove?}")]
    public async Task<ActionResult> RemoveFromCart(int productId, int quantityToRemove = 1)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var cartItem = await _context.CartItems
            .FirstOrDefaultAsync(c => c.UserId == userId && c.ProductId == productId);

        if (cartItem == null)
        {
            return NotFound("المنتج غير موجود في السلة.");
        }

        if (quantityToRemove <= 0)
        {
            return BadRequest("يجب أن تكون الكمية المطلوب إزالتها أكبر من الصفر.");
        }

        if (cartItem.Quantity <= quantityToRemove)
        {
            _context.CartItems.Remove(cartItem);
        }
        else
        {
            cartItem.Quantity -= quantityToRemove;
        }

        await _context.SaveChangesAsync();
        return Ok("تم تحديث السلة بنجاح.");
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<CartItemWithDiscountDto>>> GetCart()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        // جلب عناصر السلة مع المنتجات وصور المنتجات
        var cartItems = await _context.CartItems
            .Where(c => c.UserId == userId)
            .Include(c => c.Product)
                .ThenInclude(p => p.Images)
            .ToListAsync();

        // جلب الخصومات النشطة
        var now = DateTime.UtcNow;
        var activeDiscounts = await _context.Discounts
            .Where(d => d.IsActive && d.StartDate <= now && d.EndDate >= now)
            .Include(d => d.Products)
            .ToListAsync();

        var result = new List<CartItemWithDiscountDto>();

        decimal subtotal = 0;
        decimal totalDiscount = 0;

        foreach (var item in cartItems)
        {
            if (item.Product == null) continue;

            // حساب السعر الأصلي
            decimal originalPrice = item.Product.Price;
            decimal originalTotal = originalPrice * item.Quantity;
            subtotal += originalTotal;

            // البحث عن الخصومات المطبقة على هذا المنتج
            var applicableDiscounts = activeDiscounts.Where(d =>
                // خصم على كل المنتجات
                d.Scope == DiscountScope.AllProducts ||
                // خصم على فئة معينة
                (d.Scope == DiscountScope.Category && d.CategoryName == item.Product.Category) ||
                // خصم على منتج محدد
                (d.Scope == DiscountScope.Product && d.Products != null && d.Products.Any(dp => dp.ProductId == item.ProductId))
            ).ToList();

            if (applicableDiscounts.Any())
            {
                // اختيار أفضل خصم (الأعلى قيمة)
                var bestDiscount = GetBestDiscount(applicableDiscounts, originalPrice);

                // حساب السعر بعد الخصم
                decimal discountedPrice = CalculateDiscountedPrice(originalPrice, bestDiscount);
                decimal discountAmount = (originalPrice - discountedPrice) * item.Quantity;
                totalDiscount += discountAmount;

                result.Add(new CartItemWithDiscountDto
                {
                    ProductId = item.ProductId,
                    Name = item.Product.Name,
                    Price = originalPrice,
                    DiscountedPrice = discountedPrice,
                    Quantity = item.Quantity,
                    Total = originalTotal,
                    DiscountedTotal = discountedPrice * item.Quantity,
                    ImageUrl = item.Product.Images?.FirstOrDefault()?.ImageUrl ?? "",
                    Stock = item.Product.Stock,
                    HasDiscount = true,
                    DiscountName = bestDiscount.Name,
                    DiscountValue = bestDiscount.Value,
                    DiscountType = bestDiscount.Type.ToString(),
                    DiscountAmount = discountAmount
                });
            }
            else
            {
                // إضافة المنتج بدون خصم
                result.Add(new CartItemWithDiscountDto
                {
                    ProductId = item.ProductId,
                    Name = item.Product.Name,
                    Price = originalPrice,
                    DiscountedPrice = originalPrice,
                    Quantity = item.Quantity,
                    Total = originalTotal,
                    DiscountedTotal = originalTotal,
                    ImageUrl = item.Product.Images?.FirstOrDefault()?.ImageUrl ?? "",
                    Stock = item.Product.Stock,
                    HasDiscount = false
                });
            }
        }

        // حساب ضريبة القيمة المضافة والمجموع النهائي
        decimal vatIncluded = Math.Round((subtotal - totalDiscount) * 0.15m / 1.15m, 2);
        decimal withoutVat = Math.Round((subtotal - totalDiscount) - vatIncluded, 2);
        decimal deliveryFee = 25m;
        decimal finalTotal = Math.Round((subtotal - totalDiscount) + deliveryFee, 2);


        return Ok(new
        {
            Items = result,
            Summary = new
            {
                SubtotalWithVat = subtotal, // المجموع الفرعي شامل الضريبة
                SubtotalWithoutVat = withoutVat, // المجموع الفرعي بدون الضريبة
                TotalDiscount = totalDiscount,
                VatAmount = vatIncluded, // قيمة الضريبة المضمنة في السعر
                DeliveryFee = deliveryFee,
                FinalTotal = finalTotal,
                ItemsCount = result.Count,
                TotalQuantity = result.Sum(i => i.Quantity)
            }
        });
    }

    [HttpPost("clear")]
    public async Task<ActionResult> ClearCart()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var cartItems = await _context.CartItems
            .Where(c => c.UserId == userId)
            .ToListAsync();

        if (cartItems.Any())
        {
            _context.CartItems.RemoveRange(cartItems);
            await _context.SaveChangesAsync();
        }

        return Ok("تم مسح السلة بنجاح");
    }

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
}