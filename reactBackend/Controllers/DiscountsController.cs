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