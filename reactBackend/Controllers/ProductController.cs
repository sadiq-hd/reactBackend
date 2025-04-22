using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using reactBackend.Data;
using reactBackend.Dtos;
using reactBackend.Models;
using reactBackend.Models.Enums;

namespace reactBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProductsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public ProductsController(ApplicationDbContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        [HttpGet("search")]
        public async Task<ActionResult<IEnumerable<Product>>> SearchProducts(
        [FromQuery] string query,
        [FromQuery] string? category = null)
        {
            var productsQuery = _context.Products
                .Include(p => p.Images)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(query))
            {
                productsQuery = productsQuery.Where(p =>
                    p.Name.Contains(query) ||
                    p.Description.Contains(query));
            }

            if (!string.IsNullOrWhiteSpace(category))
            {
                productsQuery = productsQuery.Where(p => p.Category == category);
            }

            var products = await productsQuery.ToListAsync();
            return Ok(products);
        }

        // GET: api/products
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Product>>> GetProducts()
        {
            return await _context.Products
                .Include(p => p.Images)
                .ToListAsync();
        }

        // GET: api/products/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Product>> GetProduct(int id)
        {
            var product = await _context.Products
                .Include(p => p.Images)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product == null)
            {
                return NotFound();
            }

            return product;
        }

        // POST: api/products
        [HttpPost]
        
        public async Task<ActionResult<Product>> CreateProduct([FromBody] CreateProductDto productDto)
        {
            var product = new Product
            {
                Category = productDto.Category,
                Name = productDto.Name,
                Price = productDto.Price,
                Stock = productDto.Stock,
                Description = productDto.Description
            };

            _context.Products.Add(product);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetProduct), new { id = product.Id }, product);
        }

        [HttpGet("test-connection")]
public async Task<IActionResult> TestConnection()
{
    try
    {
        bool canConnect = await _context.Database.CanConnectAsync();
        return Ok(new { connected = canConnect });
    }
    catch (Exception ex)
    {
        return StatusCode(500, new { error = ex.Message, stack = ex.StackTrace });
    }
}

        // POST: api/products/{id}/images
        [HttpPost("{id}/images")]
        [Authorize]
        public async Task<ActionResult<ProductImage>> UploadImage(int id, IFormFile image)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null)
            {
                return NotFound();
            }

            if (image == null || image.Length == 0)
            {
                return BadRequest("No image file provided");
            }

            // التحقق من حجم الملف (2MB كحد أقصى)
            if (image.Length > 2 * 1024 * 1024)
            {
                return BadRequest("File size exceeds 2MB limit");
            }

            // التحقق من نوع الملف
            var allowedTypes = new[] { "image/jpeg", "image/jpg" , "image/png", "image/gif" , "image/webp" };
            if (!allowedTypes.Contains(image.ContentType.ToLower()))
            {
                return BadRequest("Invalid file type. Only JPEG, PNG and GIF are allowed");
            }

            var uniqueFileName = Guid.NewGuid().ToString() + "_" + image.FileName;
            var uploadsFolder = Path.Combine(_environment.WebRootPath, "images");
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await image.CopyToAsync(fileStream);
            }

            var productImage = new ProductImage
            {
                ProductId = id,
                ImageUrl = $"/images/{uniqueFileName}"
            };

            _context.ProductImages.Add(productImage);
            await _context.SaveChangesAsync();

            return Ok(productImage);
        }
        [HttpDelete("images/{imageId}")]
        [Authorize]
        public async Task<IActionResult> DeleteImage(int imageId)
        {
            var image = await _context.ProductImages.FindAsync(imageId);
            if (image == null)
            {
                return NotFound();
            }

            // حذف الملف الفعلي
            var filePath = Path.Combine(_environment.WebRootPath, "images",
                Path.GetFileName(image.ImageUrl));
            if (System.IO.File.Exists(filePath))
            {
                System.IO.File.Delete(filePath);
            }

            _context.ProductImages.Remove(image);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // PUT: api/products/5
        [HttpPut("{id}")]
        [Authorize]
        public async Task<IActionResult> UpdateProduct(int id, CreateProductDto productDto)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null)
            {
                return NotFound();
            }

            product.Category = productDto.Category;
            product.Name = productDto.Name;
            product.Price = productDto.Price;
            product.Stock = productDto.Stock;
            product.Description = productDto.Description;

            await _context.SaveChangesAsync();

            return NoContent();
        }


       

        [HttpGet("top-selling")]
        public async Task<ActionResult<object>> GetTopSellingProducts([FromQuery] int limit = 5)
        {
            try
            {
                var topProducts = await _context.OrderItems
                    .Where(oi => oi.Order.Status != OrderStatus.Cancelled) // نستثني الطلبات الملغية
                    .GroupBy(oi => new { oi.Product.Id, oi.Product.Name })
                    .Select(g => new
                    {
                        name = g.Key.Name,
                        sales = g.Sum(x => x.Quantity),
                        revenue = g.Sum(x => x.Total) // نستخدم Total بدلاً من Price * Quantity
                    })
                    .OrderByDescending(x => x.sales)
                    .Take(limit)
                    .ToListAsync();

                return Ok(topProducts);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
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
                    .Where(o => o.Status != OrderStatus.Cancelled)
                    .AsQueryable();

                if (startDate.HasValue)
                    query = query.Where(o => o.OrderDate >= startDate.Value);
                if (endDate.HasValue)
                    query = query.Where(o => o.OrderDate <= endDate.Value);

                // نجلب البيانات أولاً
                var orders = await query
                    .Select(o => new
                    {
                        o.OrderDate,
                        Total = o.TotalAmount + o.DeliveryFee,
                        o.SubTotal,
                        o.VatAmount,
                        o.DeliveryFee
                    })
                    .ToListAsync();

                // ثم نقوم بالتجميع في الذاكرة
                var analytics = orders
                    .GroupBy(o => o.OrderDate.Date)
                    .Select(g => new
                    {
                        date = g.Key.ToString("yyyy-MM-dd"),
                        sales = g.Count(),
                        revenue = g.Sum(o => o.Total),
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

        [HttpGet("stock-by-category")]
        [Authorize]
        public async Task<ActionResult<object>> GetStockByCategory()
        {
            try
            {
                var stockByCategory = await _context.Products
                    .GroupBy(p => p.Category)
                    .Select(g => new
                    {
                        category = g.Key,
                        totalStock = g.Sum(p => p.Stock),
                        totalValue = g.Sum(p => p.Stock * p.Price),
                        productsCount = g.Count(),
                        lowStockCount = g.Count(p => p.Stock < 10),
                        outOfStockCount = g.Count(p => p.Stock == 0)
                    })
                    .ToListAsync();

                return Ok(stockByCategory);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        // 1. الإحصائيات المتعلقة بالمنتجات فقط
        [HttpGet("stats")]
        public async Task<ActionResult<object>> GetProductStats()
        {
            var stats = new
            {
                totalProducts = await _context.Products.CountAsync(),
                totalStock = await _context.Products.SumAsync(p => p.Stock),
                totalValue = await _context.Products.SumAsync(p => p.Price * p.Stock),
                lowStockProducts = await _context.Products.CountAsync(p => p.Stock < 10),
                outOfStockProducts = await _context.Products.CountAsync(p => p.Stock == 0),
                productsByCategory = await _context.Products
                    .GroupBy(p => p.Category)
                    .Select(g => new {
                        category = g.Key,
                        count = g.Count(),
                        stockValue = g.Sum(p => p.Stock * p.Price)
                    })
                    .ToListAsync()
            };
            return Ok(stats);
        }

        // GET: api/products
        // تعديل في ProductsController.cs
        [HttpGet("with-discounts")]
        [AllowAnonymous]
        public async Task<ActionResult<IEnumerable<ProductWithDiscountDto>>> GetProductsWithDiscounts()
        {
            var now = DateTime.UtcNow;

            // الحصول على جميع المنتجات
            var products = await _context.Products
                .Include(p => p.Images)
                .ToListAsync();

            // الحصول على جميع الخصومات النشطة
            var activeDiscounts = await _context.Discounts
                .Where(d => d.IsActive && d.StartDate <= now && d.EndDate >= now)
                .Include(d => d.Products)
                .ThenInclude(dp => dp.Product)
                .ToListAsync();

            var result = new List<ProductWithDiscountDto>();

            foreach (var product in products)
            {
                // البحث عن الخصومات المطبقة على هذا المنتج
                var applicableDiscounts = activeDiscounts.Where(d =>
                    // خصم على كل المنتجات
                    d.Scope == DiscountScope.AllProducts ||
                    // خصم على فئة معينة
                    (d.Scope == DiscountScope.Category && d.CategoryName == product.Category) ||
                    // خصم على منتج محدد
                    (d.Scope == DiscountScope.Product && d.Products.Any(dp => dp.ProductId == product.Id))
                ).ToList();

                if (applicableDiscounts.Any())
                {
                    // اختيار أفضل خصم (الأعلى قيمة)
                    var bestDiscount = GetBestDiscount(applicableDiscounts, product.Price);

                    // حساب السعر بعد الخصم
                    decimal discountedPrice = CalculateDiscountedPrice(product.Price, bestDiscount);

                    // إضافة المنتج مع معلومات الخصم
                    result.Add(new ProductWithDiscountDto
                    {
                        Id = product.Id,
                        Name = product.Name,
                        Description = product.Description,
                        Price = product.Price,
                        Stock = product.Stock,
                        Category = product.Category,
                        Images = product.Images?.Select(i => i.ImageUrl).ToList(),
                        HasDiscount = true,
                        DiscountedPrice = discountedPrice,
                        DiscountName = bestDiscount.Name,
                        DiscountValue = bestDiscount.Value,
                        DiscountType = bestDiscount.Type.ToString()
                    });
                }
                else
                {
                    // إضافة المنتج بدون خصم
                    result.Add(new ProductWithDiscountDto
                    {
                        Id = product.Id,
                        Name = product.Name,
                        Description = product.Description,
                        Price = product.Price,
                        Stock = product.Stock,
                        Category = product.Category,
                        Images = product.Images?.Select(i => i.ImageUrl).ToList(),
                        HasDiscount = false
                    });
                }
            }

            return Ok(result);
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
                return originalPrice * (1 - discount.Value / 100);
            }
            return Math.Max(0, originalPrice - discount.Value);
        }



        // 3. المنتجات منخفضة المخزون
        [HttpGet("low-stock")]
        public async Task<ActionResult<object>> GetLowStockProducts([FromQuery] int threshold = 10)
        {
            var products = await _context.Products
                .Where(p => p.Stock <= threshold)
                .Select(p => new {
                    p.Id,
                    p.Name,
                    p.Stock,
                    p.Category
                })
                .ToListAsync();
            return Ok(products);
        }

        // إضافة هذه الدوال إلى ProductsController.cs

        // هذه الدالة لجلب المنتجات مع التقييمات
        [HttpGet("with-ratings")]
        public async Task<ActionResult<IEnumerable<object>>> GetProductsWithRatings()
        {
            try
            {
                var products = await _context.Products
                    .Include(p => p.Images)
                    .Include(p => p.Reviews)
                    .Select(p => new
                    {
                        p.Id,
                        p.Name,
                        p.Category,
                        p.Description,
                        p.Price,
                        p.Stock,
                        Images = p.Images.Select(i => i.ImageUrl).ToList(),
                        AverageRating = p.Reviews.Any() ? Math.Round(p.Reviews.Average(r => r.Rating), 1) : 0,
                        TotalReviews = p.Reviews.Count
                    })
                    .ToListAsync();

                return Ok(products);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "حدث خطأ أثناء جلب المنتجات", message = ex.Message });
            }
        }

        // هذه الدالة لجلب منتج واحد مع التقييمات والتعليقات
        [HttpGet("{id}/with-ratings")]
        public async Task<ActionResult<object>> GetProductWithRatings(int id)
        {
            try
            {
                var product = await _context.Products
                    .Include(p => p.Images)
                    .Include(p => p.Reviews)
                        .ThenInclude(r => r.User)
                    .FirstOrDefaultAsync(p => p.Id == id);

                if (product == null)
                {
                    return NotFound("المنتج غير موجود");
                }

                // حساب متوسط التقييم
                var averageRating = product.Reviews.Any() ? Math.Round(product.Reviews.Average(r => r.Rating), 1) : 0;

                // حساب توزيع التقييمات
                var ratingDistribution = new Dictionary<int, int>();
                for (int i = 1; i <= 5; i++)
                {
                    ratingDistribution[i] = product.Reviews.Count(r => r.Rating == i);
                }

                // جلب أحدث 5 تقييمات
                var latestReviews = product.Reviews
                    .OrderByDescending(r => r.CreatedAt)
                    .Take(5)
                    .Select(r => new
                    {
                        r.Id,
                        r.Rating,
                        r.Comment,
                        r.CreatedAt,
                        r.IsVerifiedPurchase,
                        UserName = r.User?.Name ?? "مستخدم"
                    })
                    .ToList();

                // إنشاء الكائن النهائي للاستجابة
                var result = new
                {
                    product.Id,
                    product.Name,
                    product.Category,
                    product.Description,
                    product.Price,
                    product.Stock,
                    Images = product.Images.Select(i => i.ImageUrl).ToList(),
                    AverageRating = averageRating,
                    TotalReviews = product.Reviews.Count,
                    RatingDistribution = ratingDistribution,
                    LatestReviews = latestReviews
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "حدث خطأ أثناء جلب المنتج", message = ex.Message });
            }
        }

        // هذه الدالة للبحث عن المنتجات وتصفيتها حسب التقييم
        [HttpGet("search-with-ratings")]
        public async Task<ActionResult<IEnumerable<object>>> SearchProductsWithRatings(
            [FromQuery] string? query = null,
            [FromQuery] string? category = null,
            [FromQuery] int? minRating = null)
        {
            try
            {
                var productsQuery = _context.Products
                    .Include(p => p.Images)
                    .Include(p => p.Reviews)
                    .AsQueryable();

                // تطبيق البحث النصي
                if (!string.IsNullOrWhiteSpace(query))
                {
                    productsQuery = productsQuery.Where(p =>
                        p.Name.Contains(query) ||
                        p.Description.Contains(query));
                }

                // تطبيق تصفية الفئة
                if (!string.IsNullOrWhiteSpace(category))
                {
                    productsQuery = productsQuery.Where(p => p.Category == category);
                }

                // تنفيذ الاستعلام للحصول على البيانات
                var products = await productsQuery.ToListAsync();

                // تصفية حسب التقييم (نقوم بذلك في الذاكرة لأن منطق التقييم معقد)
                var result = products
                    .Select(p => new
                    {
                        p.Id,
                        p.Name,
                        p.Category,
                        p.Description,
                        p.Price,
                        p.Stock,
                        Images = p.Images.Select(i => i.ImageUrl).ToList(),
                        AverageRating = p.Reviews.Any() ? Math.Round(p.Reviews.Average(r => r.Rating), 1) : 0,
                        TotalReviews = p.Reviews.Count
                    });

                // تطبيق تصفية الحد الأدنى للتقييم
                if (minRating.HasValue)
                {
                    result = result.Where(p => p.AverageRating >= minRating.Value);
                }

                return Ok(result.ToList());
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "حدث خطأ أثناء البحث في المنتجات", message = ex.Message });
            }
        }

        // دالة لتحديث التقييم المتوسط للمنتج (تُستخدم عند إضافة أو تعديل التقييمات)
        [HttpPut("{productId}/rating-summary")]  // أضف هذا السطر
        [Authorize]
        public async Task<IActionResult> UpdateProductRatingSummary(int productId)
        {
            try
            {
                var product = await _context.Products
                    .Include(p => p.Reviews)
                    .FirstOrDefaultAsync(p => p.Id == productId);

                if (product == null)
                {
                    return NotFound("المنتج غير موجود");
                }

                // حساب متوسط التقييم
                product.AverageRating = product.Reviews.Any() ?
                    Convert.ToDecimal(Math.Round(product.Reviews.Average(r => r.Rating), 1)) :
                    0m; product.TotalReviews = product.Reviews.Count;

                await _context.SaveChangesAsync();

                return Ok(new { averageRating = product.AverageRating, totalReviews = product.TotalReviews });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "حدث خطأ أثناء تحديث ملخص التقييم", message = ex.Message });
            }
        }

        [HttpGet("verify-images")]
        public IActionResult VerifyImages()
        {
            var imagesPath = Path.Combine(_environment.WebRootPath, "images");
            var exists = Directory.Exists(imagesPath);
            var files = exists ? Directory.GetFiles(imagesPath).Select(Path.GetFileName).ToList() : new List<string>();

            return Ok(new
            {
                imagesPathExists = exists,
                imagesCount = files.Count,
                sampleImages = files.Take(5).ToList(),
                webRootPath = _environment.WebRootPath
            });
        }

        // DELETE: api/products/5
        [HttpDelete("{id}")]
        [Authorize]
        public async Task<IActionResult> DeleteProduct(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null)
            {
                return NotFound();
            }

            // حذف الصور المرتبطة من الملفات
            var images = await _context.ProductImages
                .Where(i => i.ProductId == id)
                .ToListAsync();

            foreach (var image in images)
            {
                var filePath = Path.Combine(_environment.WebRootPath, "images",
                    Path.GetFileName(image.ImageUrl));
                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                }
            }

            _context.Products.Remove(product);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}
