// Controllers/PromoCodesController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using reactBackend.Data;
using reactBackend.Models;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
[ApiController]
[Route("api/[controller]")]
public class PromoCodesController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<PromoCodesController> _logger;

    public PromoCodesController(ApplicationDbContext context, ILogger<PromoCodesController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    [Authorize(Roles = "admin")]    
    public async Task<ActionResult<IEnumerable<PromoCodeResponseDto>>> GetPromoCodes()
    {
        var promoCodes = await _context.PromoCodes
            .Include(p => p.Usages)
            .ToListAsync();

        var responseDtos = promoCodes.Select(p => new PromoCodeResponseDto
        {
            Id = p.Id,
            Code = p.Code,
            Description = p.Description,
            Type = p.Type.ToString(),
            Value = p.Value,
            MinimumOrderAmount = p.MinimumOrderAmount,
            MaxUsesTotal = p.MaxUsesTotal,
            MaxUsesPerUser = p.MaxUsesPerUser,
            StartDate = p.StartDate,
            EndDate = p.EndDate,
            IsActive = p.IsActive,
            CreatedAt = p.CreatedAt,
            UsageCount = p.Usages?.Count ?? 0
        }).ToList();

        return Ok(responseDtos);
    }

    [HttpGet("{id}")]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult<PromoCodeResponseDto>> GetPromoCode(int id)
    {
        var promoCode = await _context.PromoCodes
            .Include(p => p.Usages)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (promoCode == null)
        {
            return NotFound();
        }

        var responseDto = new PromoCodeResponseDto
        {
            Id = promoCode.Id,
            Code = promoCode.Code,
            Description = promoCode.Description,
            Type = promoCode.Type.ToString(),
            Value = promoCode.Value,
            MinimumOrderAmount = promoCode.MinimumOrderAmount,
            MaxUsesTotal = promoCode.MaxUsesTotal,
            MaxUsesPerUser = promoCode.MaxUsesPerUser,
            StartDate = promoCode.StartDate,
            EndDate = promoCode.EndDate,
            IsActive = promoCode.IsActive,
            CreatedAt = promoCode.CreatedAt,
            UsageCount = promoCode.Usages?.Count ?? 0
        };

        return Ok(responseDto);
    }

    [HttpPost]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult<PromoCodeResponseDto>> CreatePromoCode(CreatePromoCodeDto dto)
    {
        // التحقق من عدم وجود كود خصم بنفس الاسم
        if (await _context.PromoCodes.AnyAsync(p => p.Code == dto.Code))
        {
            return BadRequest(new { error = "كود الخصم موجود بالفعل" });
        }

        var promoCode = new PromoCode
        {
            Code = dto.Code.ToUpper(),
            Description = dto.Description,
            Type = dto.Type,
            Value = dto.Value,
            MinimumOrderAmount = dto.MinimumOrderAmount,
            MaxUsesTotal = dto.MaxUsesTotal,
            MaxUsesPerUser = dto.MaxUsesPerUser,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            IsActive = dto.IsActive,
            Usages = new List<PromoCodeUsage>()
        };

        _context.PromoCodes.Add(promoCode);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetPromoCode), new { id = promoCode.Id }, new PromoCodeResponseDto
        {
            Id = promoCode.Id,
            Code = promoCode.Code,
            Description = promoCode.Description,
            Type = promoCode.Type.ToString(),
            Value = promoCode.Value,
            MinimumOrderAmount = promoCode.MinimumOrderAmount,
            MaxUsesTotal = promoCode.MaxUsesTotal,
            MaxUsesPerUser = promoCode.MaxUsesPerUser,
            StartDate = promoCode.StartDate,
            EndDate = promoCode.EndDate,
            IsActive = promoCode.IsActive,
            CreatedAt = promoCode.CreatedAt,
            UsageCount = 0
        });
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> UpdatePromoCode(int id, CreatePromoCodeDto dto)
    {
        var promoCode = await _context.PromoCodes.FindAsync(id);
        if (promoCode == null)
        {
            return NotFound();
        }

        // التحقق من عدم تكرار الكود
        if (await _context.PromoCodes.AnyAsync(p => p.Code == dto.Code && p.Id != id))
        {
            return BadRequest(new { error = "كود الخصم موجود بالفعل" });
        }

        promoCode.Code = dto.Code.ToUpper();
        promoCode.Description = dto.Description;
        promoCode.Type = dto.Type;
        promoCode.Value = dto.Value;
        promoCode.MinimumOrderAmount = dto.MinimumOrderAmount;
        promoCode.MaxUsesTotal = dto.MaxUsesTotal;
        promoCode.MaxUsesPerUser = dto.MaxUsesPerUser;
        promoCode.StartDate = dto.StartDate;
        promoCode.EndDate = dto.EndDate;
        promoCode.IsActive = dto.IsActive;

        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> DeletePromoCode(int id)
    {
        var promoCode = await _context.PromoCodes.FindAsync(id);
        if (promoCode == null)
        {
            return NotFound();
        }

        _context.PromoCodes.Remove(promoCode);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpPost("validate")]
    [Authorize]
    public async Task<ActionResult<PromoCodeValidationResult>> ValidatePromoCode(ValidatePromoCodeDto dto)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null)
        {
            return Unauthorized();
        }

        var promoCode = await _context.PromoCodes
            .Include(p => p.Usages)
            .FirstOrDefaultAsync(p => p.Code == dto.Code.ToUpper());

        if (promoCode == null)
        {
            return Ok(new PromoCodeValidationResult
            {
                IsValid = false,
                Message = "كود الخصم غير موجود"
            });
        }

        // التحقق من صلاحية الكود
        if (!promoCode.IsActive)
        {
            return Ok(new PromoCodeValidationResult
            {
                IsValid = false,
                Message = "كود الخصم غير نشط"
            });
        }

        var now = DateTime.UtcNow;
        if (now < promoCode.StartDate || now > promoCode.EndDate)
        {
            return Ok(new PromoCodeValidationResult
            {
                IsValid = false,
                Message = "كود الخصم غير ساري في هذا التاريخ"
            });
        }

        // التحقق من الحد الأدنى للطلب
        if (promoCode.MinimumOrderAmount.HasValue && dto.OrderTotal < promoCode.MinimumOrderAmount.Value)
        {
            return Ok(new PromoCodeValidationResult
            {
                IsValid = false,
                Message = $"الحد الأدنى للطلب لاستخدام هذا الكود هو {promoCode.MinimumOrderAmount.Value} ريال"
            });
        }

        // التحقق من الحد الأقصى للاستخدام الكلي
        if (promoCode.MaxUsesTotal.HasValue && promoCode.Usages.Count >= promoCode.MaxUsesTotal.Value)
        {
            return Ok(new PromoCodeValidationResult
            {
                IsValid = false,
                Message = "تم استنفاد الحد الأقصى لاستخدام هذا الكود"
            });
        }

        // التحقق من الحد الأقصى لاستخدام المستخدم
        if (promoCode.MaxUsesPerUser.HasValue)
        {
            var userUsageCount = promoCode.Usages.Count(u => u.UserId == userId);
            if (userUsageCount >= promoCode.MaxUsesPerUser.Value)
            {
                return Ok(new PromoCodeValidationResult
                {
                    IsValid = false,
                    Message = "لقد تجاوزت الحد الأقصى لاستخدام هذا الكود"
                });
            }
        }

        // حساب قيمة الخصم
        decimal discountAmount = 0;
        if (promoCode.Type == PromoCodeType.Percentage)
        {
            discountAmount = dto.OrderTotal * (promoCode.Value / 100);
        }
        else
        {
            discountAmount = promoCode.Value;
        }

        // الخصم لا يمكن أن يكون أكبر من قيمة الطلب
        discountAmount = Math.Min(discountAmount, dto.OrderTotal);

        return Ok(new PromoCodeValidationResult
        {
            IsValid = true,
            Message = "كود الخصم صالح",
            DiscountAmount = discountAmount,
            PromoCode = new PromoCodeResponseDto
            {
                Id = promoCode.Id,
                Code = promoCode.Code,
                Description = promoCode.Description,
                Type = promoCode.Type.ToString(),
                Value = promoCode.Value,
                StartDate = promoCode.StartDate,
                EndDate = promoCode.EndDate
            }
        });
    }
}