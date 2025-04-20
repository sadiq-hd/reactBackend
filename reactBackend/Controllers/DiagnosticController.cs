using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using reactBackend.Data;
using reactBackend.Dtos;
using reactBackend.Models;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

[Route("api/[controller]")]
[ApiController]
public class DiagnosticController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<DiagnosticController> _logger;
    
    public DiagnosticController(ApplicationDbContext context, ILogger<DiagnosticController> logger)
    {
        _context = context;
        _logger = logger;
    }
    
    [HttpGet("simple-test")]
    public IActionResult SimpleTest()
    {
        try
        {
            // إرجاع بيانات ثابتة بدون الوصول إلى قاعدة البيانات
            return Ok(new { message = "تم الوصول إلى نقطة النهاية بنجاح بدون الوصول لقاعدة البيانات" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطأ في اختبار بسيط");
            return StatusCode(500, new { error = ex.Message });
        }
    }
    
    [HttpGet("db-connection")]
    public async Task<IActionResult> TestDbConnection()
    {
        try
        {
            // اختبار الاتصال فقط
            bool canConnect = await _context.Database.CanConnectAsync();
            return Ok(new { canConnect });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطأ في اختبار الاتصال");
            return StatusCode(500, new { error = ex.Message, innerError = ex.InnerException?.Message });
        }
    }
    
    [HttpGet("simple-query")]
    public async Task<IActionResult> SimpleQuery()
    {
        try
        {
            // محاولة استرجاع عدد المستخدمين فقط
            var userCount = await _context.Users.CountAsync();
            return Ok(new { userCount });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطأ في استعلام بسيط");
            return StatusCode(500, new { error = ex.Message, stack = ex.StackTrace, innerError = ex.InnerException?.Message });
        }
    }
}