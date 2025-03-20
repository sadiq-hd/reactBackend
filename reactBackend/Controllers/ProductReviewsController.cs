using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using reactBackend.Data;
using reactBackend.Dtos;
using reactBackend.Models;
using reactBackend.Models.Enums;
using System.Security.Claims;

namespace reactBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProductReviewsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ProductReviewsController> _logger;

        public ProductReviewsController(ApplicationDbContext context, ILogger<ProductReviewsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // الحصول على تقييمات منتج معين
        [HttpGet("product/{productId}")]
        public async Task<ActionResult<IEnumerable<ReviewResponseDto>>> GetProductReviews(
            int productId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            try
            {
                // التحقق من وجود المنتج
                var product = await _context.Products.FindAsync(productId);
                if (product == null)
                {
                    return NotFound("المنتج غير موجود");
                }

                // التحقق من صحة قيم الصفحة وحجم الصفحة
                page = Math.Max(1, page);
                pageSize = Math.Clamp(pageSize, 1, 50);

                // حساب المجموع الكلي للتقييمات
                var totalReviews = await _context.ProductReviews
                    .Where(r => r.ProductId == productId)
                    .CountAsync();

                // جلب التقييمات مع تفاصيل المستخدم
                var reviews = await _context.ProductReviews
                    .Where(r => r.ProductId == productId)
                    .Include(r => r.User)
                    .OrderByDescending(r => r.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(r => new ReviewResponseDto
                    {
                        Id = r.Id,
                        ProductId = r.ProductId,
                        UserId = r.UserId,
                        UserName = r.User.Name,
                        Rating = r.Rating,
                        Comment = r.Comment,
                        CreatedAt = r.CreatedAt,
                        UpdatedAt = r.UpdatedAt,
                        IsVerifiedPurchase = r.IsVerifiedPurchase
                    })
                    .ToListAsync();

                // حساب ملخص التقييمات
                var summary = await GetProductReviewSummary(productId);

                // إنشاء الاستجابة
                var response = new
                {
                    Reviews = reviews,
                    Summary = summary,
                    Pagination = new
                    {
                        CurrentPage = page,
                        PageSize = pageSize,
                        TotalItems = totalReviews,
                        TotalPages = (int)Math.Ceiling(totalReviews / (double)pageSize)
                    }
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "حدث خطأ أثناء جلب تقييمات المنتج");
                return StatusCode(500, "حدث خطأ أثناء معالجة الطلب");
            }
        }

        // إضافة تقييم جديد للمنتج
        [HttpPost]
        [Authorize]
        public async Task<ActionResult<ReviewResponseDto>> AddReview([FromBody] CreateReviewDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized("يجب تسجيل الدخول لإضافة تقييم");
                }

                // التحقق من وجود المنتج
                var product = await _context.Products.FindAsync(dto.ProductId);
                if (product == null)
                {
                    return NotFound("المنتج غير موجود");
                }

                // التحقق ما إذا كان المستخدم قد قام بالتقييم مسبقاً
                var existingReview = await _context.ProductReviews
                    .FirstOrDefaultAsync(r => r.ProductId == dto.ProductId && r.UserId == userId);

                if (existingReview != null)
                {
                    return Conflict("لقد قمت بالفعل بتقييم هذا المنتج. يمكنك تحديث تقييمك الحالي");
                }

                // التحقق ما إذا كان المستخدم قد اشترى المنتج (للتقييم المعتمد)
                bool isVerifiedPurchase = await _context.OrderItems
                    .AnyAsync(oi => oi.ProductId == dto.ProductId &&
                             oi.Order.UserId == userId &&
                             oi.Order.Status == OrderStatus.Delivered);

                var review = new ProductReview
                {
                    ProductId = dto.ProductId,
                    UserId = userId,
                    Rating = dto.Rating,
                    Comment = dto.Comment,
                    CreatedAt = DateTime.UtcNow,
                    IsVerifiedPurchase = isVerifiedPurchase
                };

                _context.ProductReviews.Add(review);
                await _context.SaveChangesAsync();

                // إعادة تحميل التقييم مع معلومات المستخدم
                await _context.Entry(review).Reference(r => r.User).LoadAsync();

                var responseDto = new ReviewResponseDto
                {
                    Id = review.Id,
                    ProductId = review.ProductId,
                    UserId = review.UserId,
                    UserName = review.User?.Name ?? "مستخدم",
                    Rating = review.Rating,
                    Comment = review.Comment,
                    CreatedAt = review.CreatedAt,
                    UpdatedAt = review.UpdatedAt,
                    IsVerifiedPurchase = review.IsVerifiedPurchase
                };

                // تحديث متوسط التقييم في المنتج (إذا كنت تخزن هذه البيانات في نموذج المنتج)
                await UpdateProductAverageRating(dto.ProductId);

                return CreatedAtAction(nameof(GetReview), new { id = review.Id }, responseDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "حدث خطأ أثناء إضافة تقييم للمنتج");
                return StatusCode(500, "حدث خطأ أثناء معالجة الطلب");
            }
        }

        // الحصول على تقييم محدد
        [HttpGet("{id}")]
        public async Task<ActionResult<ReviewResponseDto>> GetReview(int id)
        {
            try
            {
                var review = await _context.ProductReviews
                    .Include(r => r.User)
                    .FirstOrDefaultAsync(r => r.Id == id);

                if (review == null)
                {
                    return NotFound("التقييم غير موجود");
                }

                var responseDto = new ReviewResponseDto
                {
                    Id = review.Id,
                    ProductId = review.ProductId,
                    UserId = review.UserId,
                    UserName = review.User?.Name ?? "مستخدم",
                    Rating = review.Rating,
                    Comment = review.Comment,
                    CreatedAt = review.CreatedAt,
                    UpdatedAt = review.UpdatedAt,
                    IsVerifiedPurchase = review.IsVerifiedPurchase
                };

                return Ok(responseDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "حدث خطأ أثناء جلب التقييم");
                return StatusCode(500, "حدث خطأ أثناء معالجة الطلب");
            }
        }

        // تحديث تقييم
        [HttpPut("{id}")]
        [Authorize]
        public async Task<IActionResult> UpdateReview(int id, [FromBody] UpdateReviewDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized("يجب تسجيل الدخول لتعديل التقييم");
                }

                var review = await _context.ProductReviews.FindAsync(id);
                if (review == null)
                {
                    return NotFound("التقييم غير موجود");
                }

                // التحقق من أن المستخدم هو صاحب التقييم
                if (review.UserId != userId && !User.IsInRole("admin"))
                {
                    return Forbid("ليس لديك صلاحية لتعديل هذا التقييم");
                }

                review.Rating = dto.Rating;
                review.Comment = dto.Comment;
                review.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                // تحديث متوسط التقييم في المنتج
                await UpdateProductAverageRating(review.ProductId);

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "حدث خطأ أثناء تعديل التقييم");
                return StatusCode(500, "حدث خطأ أثناء معالجة الطلب");
            }
        }

        // حذف تقييم
        [HttpDelete("{id}")]
        [Authorize]
        public async Task<IActionResult> DeleteReview(int id)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized("يجب تسجيل الدخول لحذف التقييم");
                }

                var review = await _context.ProductReviews.FindAsync(id);
                if (review == null)
                {
                    return NotFound("التقييم غير موجود");
                }

                // التحقق من أن المستخدم هو صاحب التقييم أو من المسؤولين
                if (review.UserId != userId && !User.IsInRole("admin"))
                {
                    return Forbid("ليس لديك صلاحية لحذف هذا التقييم");
                }

                var productId = review.ProductId;

                _context.ProductReviews.Remove(review);
                await _context.SaveChangesAsync();

                // تحديث متوسط التقييم في المنتج
                await UpdateProductAverageRating(productId);

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "حدث خطأ أثناء حذف التقييم");
                return StatusCode(500, "حدث خطأ أثناء معالجة الطلب");
            }
        }

        // الحصول على ملخص تقييمات المنتج
        [HttpGet("product/{productId}/summary")]
        public async Task<ActionResult<ReviewSummaryDto>> GetProductReviewSummaryEndpoint(int productId)
        {
            try
            {
                // التحقق من وجود المنتج
                var product = await _context.Products.FindAsync(productId);
                if (product == null)
                {
                    return NotFound("المنتج غير موجود");
                }

                var summary = await GetProductReviewSummary(productId);
                return Ok(summary);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "حدث خطأ أثناء جلب ملخص تقييمات المنتج");
                return StatusCode(500, "حدث خطأ أثناء معالجة الطلب");
            }
        }

        // التحقق ما إذا كان المستخدم قد قام بتقييم المنتج
        [HttpGet("user/reviewed/{productId}")]
        [Authorize]
        public async Task<ActionResult<bool>> HasUserReviewedProduct(int productId)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized();
                }

                var hasReviewed = await _context.ProductReviews
                    .AnyAsync(r => r.ProductId == productId && r.UserId == userId);

                return Ok(new { HasReviewed = hasReviewed });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "حدث خطأ أثناء التحقق من تقييم المستخدم للمنتج");
                return StatusCode(500, "حدث خطأ أثناء معالجة الطلب");
            }
        }


        // الحصول على جميع التقييمات للمسؤول
        [HttpGet("admin/all")]
        [Authorize(Roles = "admin")]
        public async Task<ActionResult<IEnumerable<ReviewResponseDto>>> GetAllReviewsAdmin(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            try
            {
                // التحقق من صحة قيم الصفحة وحجم الصفحة
                page = Math.Max(1, page);
                pageSize = Math.Clamp(pageSize, 1, 100);

                // حساب المجموع الكلي للتقييمات
                var totalReviews = await _context.ProductReviews.CountAsync();

                // جلب التقييمات مع معلومات المستخدم والمنتج
                var reviews = await _context.ProductReviews
                    .Include(r => r.User)
                    .Include(r => r.Product)
                    .OrderByDescending(r => r.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(r => new ReviewResponseDto
                    {
                        Id = r.Id,
                        ProductId = r.ProductId,
                        UserId = r.UserId,
                        UserName = r.User != null ? r.User.Name : "مستخدم",
                        Rating = r.Rating,
                        Comment = r.Comment,
                        CreatedAt = r.CreatedAt,
                        UpdatedAt = r.UpdatedAt,
                        IsVerifiedPurchase = r.IsVerifiedPurchase,
                        ProductName = r.Product != null ? r.Product.Name : $"منتج #{r.ProductId}"
                    })
                    .ToListAsync();

                // حساب متوسط التقييمات
                double averageRating = 0;
                if (totalReviews > 0)
                {
                    averageRating = await _context.ProductReviews
                        .AverageAsync(r => (double)r.Rating);
                }

                // توزيع التقييمات
                var distribution = new Dictionary<int, int>();
                for (int i = 1; i <= 5; i++)
                {
                    var count = await _context.ProductReviews
                        .CountAsync(r => r.Rating == i);
                    distribution[i] = count;
                }

                // إنشاء ملخص التقييمات
                var summary = new ReviewSummaryDto
                {
                    ProductId = 0, // هذا ملخص عام وليس لمنتج محدد
                    AverageRating = Math.Round(averageRating, 1),
                    TotalReviews = totalReviews,
                    RatingDistribution = distribution
                };

                // إنشاء معلومات الصفحات
                var paginationInfo = new
                {
                    CurrentPage = page,
                    PageSize = pageSize,
                    TotalItems = totalReviews,
                    TotalPages = (int)Math.Ceiling(totalReviews / (double)pageSize)
                };

                // إنشاء الاستجابة النهائية
                var responseObj = new
                {
                    Reviews = reviews,
                    Summary = summary,
                    Pagination = paginationInfo
                };

                return Ok(responseObj);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "حدث خطأ أثناء جلب جميع التقييمات للمسؤول");
                return StatusCode(500, "حدث خطأ أثناء معالجة الطلب");
            }
        }
        // الحصول على تقييمات المستخدم
        [HttpGet("user")]
        [Authorize]
        public async Task<ActionResult<IEnumerable<ReviewResponseDto>>> GetUserReviews(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized();
                }

                // التحقق من صحة قيم الصفحة وحجم الصفحة
                page = Math.Max(1, page);
                pageSize = Math.Clamp(pageSize, 1, 50);

                // حساب المجموع الكلي للتقييمات
                var totalReviews = await _context.ProductReviews
                    .Where(r => r.UserId == userId)
                    .CountAsync();

                // جلب التقييمات
                var reviews = await _context.ProductReviews
                    .Where(r => r.UserId == userId)
                    .Include(r => r.Product)
                    .OrderByDescending(r => r.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(r => new ReviewResponseDto
                    {
                        Id = r.Id,
                        ProductId = r.ProductId,
                        UserId = r.UserId,
                        UserName = "", // لا نحتاج لاسم المستخدم هنا
                        Rating = r.Rating,
                        Comment = r.Comment,
                        CreatedAt = r.CreatedAt,
                        UpdatedAt = r.UpdatedAt,
                        IsVerifiedPurchase = r.IsVerifiedPurchase,
                        // إضافة بعض معلومات المنتج
                        ProductName = r.Product.Name,
                        ProductImage = r.Product.Images.Any() ? r.Product.Images.First().ImageUrl : ""
                    })
                    .ToListAsync();

                // إنشاء الاستجابة
                var response = new
                {
                    Reviews = reviews,
                    Pagination = new
                    {
                        CurrentPage = page,
                        PageSize = pageSize,
                        TotalItems = totalReviews,
                        TotalPages = (int)Math.Ceiling(totalReviews / (double)pageSize)
                    }
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "حدث خطأ أثناء جلب تقييمات المستخدم");
                return StatusCode(500, "حدث خطأ أثناء معالجة الطلب");
            }
        }

        // دالة مساعدة لحساب ملخص تقييمات المنتج
        private async Task<ReviewSummaryDto> GetProductReviewSummary(int productId)
        {
            var reviews = await _context.ProductReviews
                .Where(r => r.ProductId == productId)
                .ToListAsync();

            var totalReviews = reviews.Count;
            var averageRating = totalReviews > 0 ? reviews.Average(r => r.Rating) : 0;

            // حساب توزيع التقييمات
            var distribution = new Dictionary<int, int>();
            for (int i = 1; i <= 5; i++)
            {
                distribution[i] = reviews.Count(r => r.Rating == i);
            }

            return new ReviewSummaryDto
            {
                ProductId = productId,
                AverageRating = Math.Round(averageRating, 1),
                TotalReviews = totalReviews,
                RatingDistribution = distribution
            };
        }

        // دالة مساعدة لتحديث متوسط التقييم في نموذج المنتج
        private async Task UpdateProductAverageRating(int productId)
        {
            try
            {
                var summary = await GetProductReviewSummary(productId);
                var product = await _context.Products.FindAsync(productId);
                if (product != null)
                {
                    // تحويل متوسط التقييم إلى نوع decimal? المتوقع
                    product.AverageRating = Convert.ToDecimal(summary.AverageRating);
                    product.TotalReviews = summary.TotalReviews;
                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"حدث خطأ أثناء تحديث متوسط تقييم المنتج {productId}");
            }
        }
    }
    }