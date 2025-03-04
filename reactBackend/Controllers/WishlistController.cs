using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using reactBackend.Data;
using reactBackend.Models;
using System.Security.Claims;

namespace reactBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class WishlistController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<WishlistController> _logger;

        public WishlistController(ApplicationDbContext context, ILogger<WishlistController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpPost("{productId}")]
        public async Task<ActionResult> AddToWishlist(int productId)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized();
                }

                // التحقق من وجود المنتج
                var product = await _context.Products.FindAsync(productId);
                if (product == null)
                {
                    return NotFound("المنتج غير موجود");
                }

                var existingItem = await _context.WishlistItems
                    .FirstOrDefaultAsync(w => w.UserId == userId && w.ProductId == productId);

                if (existingItem != null)
                {
                    return BadRequest("المنتج موجود بالفعل في المفضلة");
                }

                var item = new WishlistItem
                {
                    UserId = userId,
                    ProductId = productId,
                    DateAdded = DateTime.UtcNow
                };

                _context.WishlistItems.Add(item);
                await _context.SaveChangesAsync();

                return Ok(new { message = "تمت إضافة المنتج إلى المفضلة" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding to wishlist");
                return StatusCode(500, "حدث خطأ أثناء إضافة المنتج إلى المفضلة");
            }
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Product>>> GetWishlist()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized();
                }

                var items = await _context.WishlistItems
                    .Where(w => w.UserId == userId)
                    .Include(w => w.Product)
                        .ThenInclude(p => p.Images)
                    .Select(w => w.Product)
                    .ToListAsync();

                return Ok(items);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching wishlist");
                return StatusCode(500, "حدث خطأ أثناء جلب قائمة المفضلة");
            }
        }

        [HttpDelete("{productId}")]
        public async Task<IActionResult> RemoveFromWishlist(int productId)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized();
                }

                var item = await _context.WishlistItems
                    .FirstOrDefaultAsync(w => w.UserId == userId && w.ProductId == productId);

                if (item == null)
                {
                    return NotFound("المنتج غير موجود في المفضلة");
                }

                _context.WishlistItems.Remove(item);
                await _context.SaveChangesAsync();

                return Ok(new { message = "تم حذف المنتج من المفضلة" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing from wishlist");
                return StatusCode(500, "حدث خطأ أثناء حذف المنتج من المفضلة");
            }
        }

        [HttpDelete("clear")]
        public async Task<IActionResult> ClearWishlist()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized();
                }

                var items = await _context.WishlistItems
                    .Where(w => w.UserId == userId)
                    .ToListAsync();

                _context.WishlistItems.RemoveRange(items);
                await _context.SaveChangesAsync();

                return Ok(new { message = "تم مسح قائمة المفضلة" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing wishlist");
                return StatusCode(500, "حدث خطأ أثناء مسح قائمة المفضلة");
            }
        }
    }
}