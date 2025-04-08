// Controllers/DiscountsController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using reactBackend.Data;
using Microsoft.EntityFrameworkCore;

namespace reactBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "admin")]
    public class DiscountsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<DiscountsController> _logger;

        public DiscountsController(ApplicationDbContext context, ILogger<DiscountsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<DiscountResponseDto>>> GetDiscounts()
        {
            var discounts = await _context.Discounts
                .Include(d => d.Products)
                .ThenInclude(dp => dp.Product)
                .ToListAsync();

            var responseDtos = discounts.Select(d => new DiscountResponseDto
            {
                Id = d.Id,
                Name = d.Name,
                Description = d.Description,
                Type = d.Type.ToString(),
                Value = d.Value,
                Scope = d.Scope.ToString(),
                CategoryName = d.CategoryName,
                Products = d.Products?.Select(dp => new ProductResponseDto
                {
                    Id = dp.ProductId,
                    Name = dp.Product.Name
                }).ToList(),
                StartDate = d.StartDate,
                EndDate = d.EndDate,
                IsActive = d.IsActive,
                CreatedAt = d.CreatedAt
            }).ToList();

            return Ok(responseDtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<DiscountResponseDto>> GetDiscount(int id)
        {
            var discount = await _context.Discounts
                .Include(d => d.Products)
                .ThenInclude(dp => dp.Product)
                .FirstOrDefaultAsync(d => d.Id == id);

            if (discount == null)
            {
                return NotFound();
            }

            var responseDto = new DiscountResponseDto
            {
                Id = discount.Id,
                Name = discount.Name,
                Description = discount.Description,
                Type = discount.Type.ToString(),
                Value = discount.Value,
                Scope = discount.Scope.ToString(),
                CategoryName = discount.CategoryName,
                Products = discount.Products?.Select(dp => new ProductResponseDto
                {
                    Id = dp.ProductId,
                    Name = dp.Product.Name
                }).ToList(),
                StartDate = discount.StartDate,
                EndDate = discount.EndDate,
                IsActive = discount.IsActive,
                CreatedAt = discount.CreatedAt
            };

            return Ok(responseDto);
        }

        [HttpPost]
        public async Task<ActionResult<DiscountResponseDto>> CreateDiscount(CreateDiscountDto dto)
        {
            _logger.LogInformation($"Creating new discount: {dto.Name}");

            var discount = new Discount
            {
                Name = dto.Name,
                Description = dto.Description,
                Type = dto.Type,
                Value = dto.Value,
                Scope = dto.Scope,
                CategoryName = dto.CategoryName,
                StartDate = dto.StartDate,
                EndDate = dto.EndDate,
                IsActive = dto.IsActive
            };

            if (dto.Scope == DiscountScope.Product && dto.ProductIds != null && dto.ProductIds.Any())
            {
                discount.Products = new List<DiscountProduct>();
                foreach (var productId in dto.ProductIds)
                {
                    var product = await _context.Products.FindAsync(productId);
                    if (product != null)
                    {
                        discount.Products.Add(new DiscountProduct
                        {
                            ProductId = productId,
                            Discount = discount
                        });
                    }
                    else
                    {
                        _logger.LogWarning($"Product with ID {productId} not found when creating discount");
                    }
                }
            }

            _context.Discounts.Add(discount);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Successfully created discount with ID: {discount.Id}");

            return CreatedAtAction(nameof(GetDiscount), new { id = discount.Id }, new DiscountResponseDto
            {
                Id = discount.Id,
                Name = discount.Name,
                Description = discount.Description,
                Type = discount.Type.ToString(),
                Value = discount.Value,
                Scope = discount.Scope.ToString(),
                CategoryName = discount.CategoryName,
                Products = discount.Products?.Select(dp => new ProductResponseDto
                {
                    Id = dp.ProductId,
                    Name = dp.Product?.Name ?? string.Empty
                }).ToList(),
                StartDate = discount.StartDate,
                EndDate = discount.EndDate,
                IsActive = discount.IsActive,
                CreatedAt = discount.CreatedAt
            });
        }

       

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateDiscount(int id, CreateDiscountDto dto)
        {
            _logger.LogInformation($"Updating discount ID: {id}");

            var discount = await _context.Discounts
                .Include(d => d.Products)
                .FirstOrDefaultAsync(d => d.Id == id);

            if (discount == null)
            {
                _logger.LogWarning($"Discount with ID {id} not found for update");
                return NotFound();
            }

            discount.Name = dto.Name;
            discount.Description = dto.Description;
            discount.Type = dto.Type;
            discount.Value = dto.Value;
            discount.Scope = dto.Scope;
            discount.CategoryName = dto.CategoryName;
            discount.StartDate = dto.StartDate;
            discount.EndDate = dto.EndDate;
            discount.IsActive = dto.IsActive;

            // تحديث المنتجات المرتبطة بالخصم
            if (discount.Products == null)
            {
                discount.Products = new List<DiscountProduct>();
            }

            if (dto.Scope == DiscountScope.Product && dto.ProductIds != null)
            {
                // حذف العلاقات القديمة من قاعدة البيانات مباشرة
                var existingRelations = await _context.DiscountProducts
                    .Where(dp => dp.DiscountId == id)
                    .ToListAsync();

                _context.DiscountProducts.RemoveRange(existingRelations);
                await _context.SaveChangesAsync();

                // إضافة العلاقات الجديدة
                foreach (var productId in dto.ProductIds)
                {
                    var product = await _context.Products.FindAsync(productId);
                    if (product != null)
                    {
                        var newRelation = new DiscountProduct
                        {
                            ProductId = productId,
                            DiscountId = discount.Id
                        };

                        _context.DiscountProducts.Add(newRelation);
                    }
                    else
                    {
                        _logger.LogWarning($"Product with ID {productId} not found when updating discount");
                    }
                }
            }
            else if (discount.Products.Any())
            {
                // إذا تغير نطاق الخصم من منتجات إلى فئة أو متجر بأكمله، نحذف جميع العلاقات
                var existingRelations = await _context.DiscountProducts
                    .Where(dp => dp.DiscountId == id)
                    .ToListAsync();

                _context.DiscountProducts.RemoveRange(existingRelations);
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation($"Successfully updated discount with ID: {id}");

            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteDiscount(int id)
        {
            var discount = await _context.Discounts
                .Include(d => d.Products)
                .FirstOrDefaultAsync(d => d.Id == id);

            if (discount == null)
            {
                return NotFound();
            }

            // فحص وإزالة العلاقات مع عناصر الطلبات
            var orderItems = await _context.OrderItems
                .Where(oi => oi.DiscountId == id)
                .ToListAsync();

            foreach (var item in orderItems)
            {
                item.DiscountId = null;
                item.DiscountName = null;
                _context.Entry(item).State = EntityState.Modified;
            }

            // إزالة العلاقات مع المنتجات
            if (discount.Products != null && discount.Products.Any())
            {
                _context.DiscountProducts.RemoveRange(discount.Products);
            }

            // حذف الخصم
            _context.Discounts.Remove(discount);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpGet("discounted-products")]
        [AllowAnonymous] // يسمح بالوصول العام للمنتجات المخفضة
        public async Task<ActionResult<IEnumerable<ProductWithDiscountDto>>> GetDiscountedProducts()
        {
            var now = DateTime.UtcNow;

            // الحصول على جميع المنتجات التي لها خصومات نشطة
            var discountedProducts = await _context.DiscountProducts
                .Where(dp => dp.Discount.IsActive &&
                             dp.Discount.StartDate <= now &&
                             dp.Discount.EndDate >= now)
                .Include(dp => dp.Product)
                .Include(dp => dp.Product.Images)
                .Include(dp => dp.Discount)
                .ToListAsync(); // استرجاع البيانات أولاً ثم إجراء التحويل

            // تحويل البيانات إلى DTO
            var result = discountedProducts.Select(dp => new ProductWithDiscountDto
            {
                Id = dp.ProductId,
                Name = dp.Product.Name,
                Description = dp.Product.Description,
                Price = dp.Product.Price,
                Stock = dp.Product.Stock,
                Category = dp.Product.Category,
                Images = dp.Product.Images.Select(img => img.ImageUrl).ToList(),

                // معلومات الخصم
                HasDiscount = true,
                DiscountedPrice = CalculateFinalPrice(dp.Product.Price, dp.Discount.Type, dp.Discount.Value),
                DiscountName = dp.Discount.Name,
                DiscountValue = dp.Discount.Value,
                DiscountType = dp.Discount.Type.ToString()
            }).ToList();

            return Ok(result);
        }

        // دالة مساعدة لحساب السعر بعد الخصم - تحويلها إلى static
        private static decimal CalculateFinalPrice(decimal originalPrice, DiscountType discountType, decimal discountValue)
        {
            if (discountType == DiscountType.Percentage)
            {
                // في حالة الخصم بالنسبة المئوية
                return Math.Round(originalPrice * (1 - (discountValue / 100)), 2);
            }
            else // DiscountType.Fixed
            {
                // في حالة الخصم بمبلغ ثابت
                return Math.Max(0, Math.Round(originalPrice - discountValue, 2));
            }
        }

        [HttpGet("active")]
        [AllowAnonymous] // يسمح بالوصول العام للخصومات النشطة
        public async Task<ActionResult<IEnumerable<DiscountResponseDto>>> GetActiveDiscounts()
        {
            var now = DateTime.UtcNow;

            var activeDiscounts = await _context.Discounts
                .Where(d => d.IsActive && d.StartDate <= now && d.EndDate >= now)
                .Include(d => d.Products)
                .ThenInclude(dp => dp.Product)
                .ToListAsync();

            var responseDtos = activeDiscounts.Select(d => new DiscountResponseDto
            {
                Id = d.Id,
                Name = d.Name,
                Description = d.Description,
                Type = d.Type.ToString(),
                Value = d.Value,
                Scope = d.Scope.ToString(),
                CategoryName = d.CategoryName,
                Products = d.Products?.Select(dp => new ProductResponseDto
                {
                    Id = dp.ProductId,
                    Name = dp.Product.Name
                }).ToList(),
                StartDate = d.StartDate,
                EndDate = d.EndDate,
                IsActive = d.IsActive,
                CreatedAt = d.CreatedAt
            }).ToList();

            return Ok(responseDtos);
        }
    }
}