using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using reactBackend.Data;
using reactBackend.Dtos;
using reactBackend.Models;
using System.Security.Claims;

namespace reactBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProductCommentsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ProductCommentsController> _logger;

        public ProductCommentsController(ApplicationDbContext context, ILogger<ProductCommentsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // الحصول على تعليقات منتج معين
        [HttpGet("product/{productId}")]
        public async Task<ActionResult<IEnumerable<CommentResponseDto>>> GetProductComments(int productId)
        {
            try
            {
                // التحقق من وجود المنتج
                var product = await _context.Products.FindAsync(productId);
                if (product == null)
                {
                    return NotFound("المنتج غير موجود");
                }

                // جلب معرف المستخدم الحالي إن وجد
                var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                // جلب التعليقات الرئيسية (غير الردود) مع معلومات المستخدم
                var comments = await _context.ProductComments
                    .Where(c => c.ProductId == productId && !c.IsDeleted && c.ParentCommentId == null)
                    .Include(c => c.User)
                    .Include(c => c.Likes)
                    .OrderByDescending(c => c.CreatedAt)
                    .ToListAsync();

                // جلب جميع الردود على التعليقات
                var allReplies = await _context.ProductComments
                    .Where(c => c.ProductId == productId && !c.IsDeleted && c.ParentCommentId != null)
                    .Include(c => c.User)
                    .Include(c => c.Likes)
                    .OrderBy(c => c.CreatedAt)
                    .ToListAsync();

                // تنظيم الردود حسب التعليق الأصلي
                var repliesByParentId = allReplies.GroupBy(r => r.ParentCommentId).ToDictionary(g => g.Key, g => g.ToList());

                // تحويل التعليقات إلى DTO مع إضافة الردود الخاصة بها
                var commentDtos = comments.Select(c => MapCommentToDto(c, currentUserId, repliesByParentId)).ToList();

                return Ok(commentDtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "حدث خطأ أثناء جلب تعليقات المنتج");
                return StatusCode(500, "حدث خطأ أثناء معالجة الطلب");
            }
        }

        // إضافة تعليق جديد
        [HttpPost]
        [Authorize]
        public async Task<ActionResult<CommentResponseDto>> AddComment([FromBody] CreateCommentDto dto)
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
                    return Unauthorized("يجب تسجيل الدخول لإضافة تعليق");
                }

                // التحقق من وجود المنتج
                var product = await _context.Products.FindAsync(dto.ProductId);
                if (product == null)
                {
                    return NotFound("المنتج غير موجود");
                }

                // التحقق من وجود التعليق الأصلي في حالة الرد
                if (dto.ParentCommentId.HasValue)
                {
                    var parentComment = await _context.ProductComments.FindAsync(dto.ParentCommentId.Value);
                    if (parentComment == null || parentComment.IsDeleted)
                    {
                        return NotFound("التعليق الأصلي غير موجود");
                    }

                    // التأكد من أن التعليق الأصلي ليس رداً بنفسه
                    if (parentComment.ParentCommentId.HasValue)
                    {
                        return BadRequest("لا يمكن الرد على رد آخر");
                    }
                }

                var comment = new ProductComment
                {
                    ProductId = dto.ProductId,
                    UserId = userId,
                    Content = dto.Content,
                    ParentCommentId = dto.ParentCommentId,
                    CreatedAt = DateTime.UtcNow
                };

                _context.ProductComments.Add(comment);
                await _context.SaveChangesAsync();

                // إعادة تحميل التعليق مع معلومات المستخدم
                await _context.Entry(comment).Reference(c => c.User).LoadAsync();

                var responseDto = new CommentResponseDto
                {
                    Id = comment.Id,
                    ProductId = comment.ProductId,
                    UserId = comment.UserId,
                    UserName = comment.User?.Name ?? "مستخدم",
                    Content = comment.Content,
                    CreatedAt = comment.CreatedAt,
                    LikesCount = 0,
                    IsLikedByCurrentUser = false,
                    ParentCommentId = comment.ParentCommentId,
                    Replies = new List<CommentResponseDto>()
                };

                return CreatedAtAction(nameof(GetComment), new { id = comment.Id }, responseDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "حدث خطأ أثناء إضافة تعليق");
                return StatusCode(500, "حدث خطأ أثناء معالجة الطلب");
            }
        }

        // الحصول على تعليق محدد
        [HttpGet("{id}")]
        public async Task<ActionResult<CommentResponseDto>> GetComment(int id)
        {
            try
            {
                var comment = await _context.ProductComments
                    .Include(c => c.User)
                    .Include(c => c.Likes)
                    .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted);

                if (comment == null)
                {
                    return NotFound("التعليق غير موجود");
                }

                var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                // جلب الردود إذا كان تعليقاً رئيسياً
                var replies = new List<ProductComment>();
                if (!comment.ParentCommentId.HasValue)
                {
                    replies = await _context.ProductComments
                        .Where(c => c.ParentCommentId == comment.Id && !c.IsDeleted)
                        .Include(c => c.User)
                        .Include(c => c.Likes)
                        .OrderBy(c => c.CreatedAt)
                        .ToListAsync();
                }

                var repliesByParentId = new Dictionary<int?, List<ProductComment>>();
                if (replies.Any())
                {
                    repliesByParentId[comment.Id] = replies;
                }

                var commentDto = MapCommentToDto(comment, currentUserId, repliesByParentId);

                return Ok(commentDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "حدث خطأ أثناء جلب التعليق");
                return StatusCode(500, "حدث خطأ أثناء معالجة الطلب");
            }
        }

        // تحديث تعليق
        [HttpPut("{id}")]
        [Authorize]
        public async Task<IActionResult> UpdateComment(int id, [FromBody] UpdateCommentDto dto)
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
                    return Unauthorized("يجب تسجيل الدخول لتعديل التعليق");
                }

                var comment = await _context.ProductComments.FindAsync(id);
                if (comment == null || comment.IsDeleted)
                {
                    return NotFound("التعليق غير موجود");
                }

                // التحقق من أن المستخدم هو صاحب التعليق
                if (comment.UserId != userId && !User.IsInRole("admin"))
                {
                    return Forbid("ليس لديك صلاحية لتعديل هذا التعليق");
                }

                comment.Content = dto.Content;
                comment.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "حدث خطأ أثناء تعديل التعليق");
                return StatusCode(500, "حدث خطأ أثناء معالجة الطلب");
            }
        }

        // حذف تعليق
        [HttpDelete("{id}")]
        [Authorize]
        public async Task<IActionResult> DeleteComment(int id)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized("يجب تسجيل الدخول لحذف التعليق");
                }

                var comment = await _context.ProductComments.FindAsync(id);
                if (comment == null || comment.IsDeleted)
                {
                    return NotFound("التعليق غير موجود");
                }

                // التحقق من أن المستخدم هو صاحب التعليق أو من المسؤولين
                if (comment.UserId != userId && !User.IsInRole("admin"))
                {
                    return Forbid("ليس لديك صلاحية لحذف هذا التعليق");
                }

                // حذف منطقي فقط - الأفضل من الحذف الفعلي
                comment.IsDeleted = true;
                comment.UpdatedAt = DateTime.UtcNow;

                // إذا كان التعليق رئيسياً، قم بحذف الردود أيضاً
                if (!comment.ParentCommentId.HasValue)
                {
                    var replies = await _context.ProductComments
                        .Where(c => c.ParentCommentId == comment.Id)
                        .ToListAsync();

                    foreach (var reply in replies)
                    {
                        reply.IsDeleted = true;
                        reply.UpdatedAt = DateTime.UtcNow;
                    }
                }

                await _context.SaveChangesAsync();

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "حدث خطأ أثناء حذف التعليق");
                return StatusCode(500, "حدث خطأ أثناء معالجة الطلب");
            }
        }

        // إضافة هذه الدالة في وحدة التحكم ProductCommentsController
        [HttpGet("admin/all")]
        [Authorize(Roles = "admin")]
        public async Task<ActionResult<IEnumerable<CommentResponseDto>>> GetAllCommentsForAdmin()
        {
            try
            {
                // جلب معرف المستخدم الحالي إن وجد
                var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                // جلب جميع التعليقات الرئيسية مع معلومات المستخدم والمنتج
                var comments = await _context.ProductComments
                    .Where(c => !c.IsDeleted && c.ParentCommentId == null)
                    .Include(c => c.User)
                    .Include(c => c.Product)
                    .Include(c => c.Likes)
                    .OrderByDescending(c => c.CreatedAt)
                    .ToListAsync();

                // جلب جميع الردود على التعليقات
                var allReplies = await _context.ProductComments
                    .Where(c => !c.IsDeleted && c.ParentCommentId != null)
                    .Include(c => c.User)
                    .Include(c => c.Likes)
                    .OrderBy(c => c.CreatedAt)
                    .ToListAsync();

                // تنظيم الردود حسب التعليق الأصلي
                var repliesByParentId = allReplies.GroupBy(r => r.ParentCommentId)
                    .ToDictionary(g => g.Key, g => g.ToList());

                // تحويل التعليقات إلى DTO مع إضافة الردود الخاصة بها
                var commentDtos = comments.Select(c => MapCommentToDto(c, currentUserId, repliesByParentId))
                    .ToList();

                return Ok(commentDtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "حدث خطأ أثناء جلب جميع التعليقات");
                return StatusCode(500, "حدث خطأ أثناء معالجة الطلب");
            }
        }

        // الإعجاب بتعليق / إلغاء الإعجاب
        [HttpPost("{id}/like")]
        [Authorize]
        public async Task<IActionResult> LikeComment(int id)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized("يجب تسجيل الدخول للإعجاب بالتعليق");
                }

                var comment = await _context.ProductComments.FindAsync(id);
                if (comment == null || comment.IsDeleted)
                {
                    return NotFound("التعليق غير موجود");
                }

                // البحث عن إعجاب موجود مسبقاً
                var existingLike = await _context.CommentLikes
                    .FirstOrDefaultAsync(l => l.CommentId == id && l.UserId == userId);

                if (existingLike != null)
                {
                    // إلغاء الإعجاب
                    _context.CommentLikes.Remove(existingLike);
                    comment.LikesCount = Math.Max(0, comment.LikesCount - 1);
                }
                else
                {
                    // إضافة إعجاب جديد
                    var like = new CommentLike
                    {
                        CommentId = id,
                        UserId = userId,
                        CreatedAt = DateTime.UtcNow
                    };

                    _context.CommentLikes.Add(like);
                    comment.LikesCount++;
                }

                await _context.SaveChangesAsync();

                return Ok(new { LikesCount = comment.LikesCount, IsLiked = existingLike == null });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "حدث خطأ أثناء معالجة الإعجاب بالتعليق");
                return StatusCode(500, "حدث خطأ أثناء معالجة الطلب");
            }
        }

        // دالة مساعدة لتحويل التعليق إلى DTO
        private CommentResponseDto MapCommentToDto(
            ProductComment comment,
            string? currentUserId,
            Dictionary<int?, List<ProductComment>> repliesByParentId)
        {
            var isLikedByCurrentUser = false;
            if (!string.IsNullOrEmpty(currentUserId) && comment.Likes != null)
            {
                isLikedByCurrentUser = comment.Likes.Any(l => l.UserId == currentUserId);
            }

            var dto = new CommentResponseDto
            {
                Id = comment.Id,
                ProductId = comment.ProductId,
                UserId = comment.UserId,
                UserName = comment.User?.Name ?? "مستخدم",
                Content = comment.Content,
                CreatedAt = comment.CreatedAt,
                UpdatedAt = comment.UpdatedAt,
                LikesCount = comment.LikesCount,
                IsLikedByCurrentUser = isLikedByCurrentUser,
                ParentCommentId = comment.ParentCommentId,
                Replies = new List<CommentResponseDto>()
            };

            // إضافة الردود إذا كان التعليق رئيسياً وتوجد ردود
            if (!comment.ParentCommentId.HasValue &&
                repliesByParentId.TryGetValue(comment.Id, out var replies))
            {
                dto.Replies = replies.Select(r => MapCommentToDto(r, currentUserId, repliesByParentId)).ToList();
            }

            return dto;
        }
    }
}