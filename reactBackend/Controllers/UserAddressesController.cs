using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using reactBackend.Data;
using reactBackend.Models;
using reactBackend.Dtos;

namespace reactBackend.Controllers
{
    [Route("api/delivery-addresses")]
    [ApiController]
    [Authorize]
    public class UserAddressesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<UserAddressesController> _logger;

        public UserAddressesController(
            ApplicationDbContext context,
            ILogger<UserAddressesController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: api/delivery-addresses
        [HttpGet]
        public async Task<ActionResult<IEnumerable<UserAddress>>> GetUserAddresses()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (userId == null)
                {
                    return Unauthorized(new { message = "المستخدم غير مصرح" });
                }

                var addresses = await _context.UserAddresses
                    .Where(a => a.UserId == userId)
                    .OrderByDescending(a => a.IsDefault)
                    .ThenByDescending(a => a.CreatedAt)
                    .ToListAsync();

                return Ok(addresses);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving addresses");
                return StatusCode(500, new { message = "حدث خطأ أثناء جلب العناوين" });
            }
        }

        // GET: api/delivery-addresses/5
        [HttpGet("{id}")]
        public async Task<ActionResult<UserAddress>> GetUserAddress(int id)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (userId == null)
                {
                    return Unauthorized(new { message = "المستخدم غير مصرح" });
                }

                var address = await _context.UserAddresses
                    .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId);

                if (address == null)
                {
                    return NotFound(new { message = "العنوان غير موجود" });
                }

                return Ok(address);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving address with ID {AddressId}", id);
                return StatusCode(500, new { message = "حدث خطأ أثناء جلب العنوان" });
            }
        }

        // POST: api/delivery-addresses
        [HttpPost]
        public async Task<ActionResult<UserAddress>> CreateUserAddress([FromBody] UserAddressDto dto)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (userId == null)
                {
                    return Unauthorized(new { message = "المستخدم غير مصرح" });
                }

                // التحقق من عدم وجود أي عنوان افتراضي إذا كان هذا العنوان سيكون افتراضي
                if (dto.IsDefault)
                {
                    var defaultAddresses = await _context.UserAddresses
                        .Where(a => a.UserId == userId && a.IsDefault)
                        .ToListAsync();

                    foreach (var addr in defaultAddresses)
                    {
                        addr.IsDefault = false;
                        _context.Entry(addr).State = EntityState.Modified;
                    }
                }

                var userAddress = new UserAddress
                {
                    UserId = userId,
                    FullName = dto.FullName.Trim(),
                    PhoneNumber = dto.PhoneNumber.Trim(),
                    City = dto.City.Trim(),
                    Street = dto.Street.Trim(),
                    BuildingNumber = dto.BuildingNumber?.Trim(),
                    AdditionalDetails = dto.AdditionalDetails?.Trim(),
                    IsDefault = dto.IsDefault,
                    CreatedAt = DateTime.UtcNow
                };

                _context.UserAddresses.Add(userAddress);
                await _context.SaveChangesAsync();

                return CreatedAtAction(nameof(GetUserAddress), new { id = userAddress.Id }, userAddress);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating address");
                return StatusCode(500, new { message = "حدث خطأ أثناء إنشاء العنوان" });
            }
        }

        // PUT: api/delivery-addresses/5
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateUserAddress(int id, [FromBody] UserAddressDto dto)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (userId == null)
                {
                    return Unauthorized(new { message = "المستخدم غير مصرح" });
                }

                var address = await _context.UserAddresses
                    .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId);

                if (address == null)
                {
                    return NotFound(new { message = "العنوان غير موجود" });
                }

                // إذا كان هذا العنوان سيكون افتراضي والعنوان الحالي ليس افتراضي
                if (dto.IsDefault && !address.IsDefault)
                {
                    var defaultAddresses = await _context.UserAddresses
                        .Where(a => a.UserId == userId && a.IsDefault)
                        .ToListAsync();

                    foreach (var addr in defaultAddresses)
                    {
                        addr.IsDefault = false;
                        _context.Entry(addr).State = EntityState.Modified;
                    }
                }

                // تحديث بيانات العنوان
                address.FullName = dto.FullName.Trim();
                address.PhoneNumber = dto.PhoneNumber.Trim();
                address.City = dto.City.Trim();
                address.Street = dto.Street.Trim();
                address.BuildingNumber = dto.BuildingNumber?.Trim();
                address.AdditionalDetails = dto.AdditionalDetails?.Trim();
                address.IsDefault = dto.IsDefault;

                _context.Entry(address).State = EntityState.Modified;
                await _context.SaveChangesAsync();

                return Ok(address);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating address with ID {AddressId}", id);
                return StatusCode(500, new { message = "حدث خطأ أثناء تحديث العنوان" });
            }
        }

        // DELETE: api/delivery-addresses/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteUserAddress(int id)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (userId == null)
                {
                    return Unauthorized(new { message = "المستخدم غير مصرح" });
                }

                var address = await _context.UserAddresses
                    .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId);

                if (address == null)
                {
                    return NotFound(new { message = "العنوان غير موجود" });
                }

                _context.UserAddresses.Remove(address);
                await _context.SaveChangesAsync();

                return Ok(new { message = "تم حذف العنوان بنجاح" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting address with ID {AddressId}", id);
                return StatusCode(500, new { message = "حدث خطأ أثناء حذف العنوان" });
            }
        }

        // PUT: api/delivery-addresses/5/set-default
        [HttpPut("{id}/set-default")]
        public async Task<IActionResult> SetDefaultAddress(int id)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (userId == null)
                {
                    return Unauthorized(new { message = "المستخدم غير مصرح" });
                }

                var address = await _context.UserAddresses
                    .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId);

                if (address == null)
                {
                    return NotFound(new { message = "العنوان غير موجود" });
                }

                // إلغاء تحديد جميع العناوين الافتراضية لهذا المستخدم
                var defaultAddresses = await _context.UserAddresses
                    .Where(a => a.UserId == userId && a.IsDefault)
                    .ToListAsync();

                foreach (var addr in defaultAddresses)
                {
                    addr.IsDefault = false;
                    _context.Entry(addr).State = EntityState.Modified;
                }

                // تعيين هذا العنوان كافتراضي
                address.IsDefault = true;
                _context.Entry(address).State = EntityState.Modified;

                await _context.SaveChangesAsync();

                return Ok(new { message = "تم تعيين العنوان الافتراضي بنجاح" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting default address with ID {AddressId}", id);
                return StatusCode(500, new { message = "حدث خطأ أثناء تعيين العنوان الافتراضي" });
            }
        }
    }
}