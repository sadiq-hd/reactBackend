using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using reactBackend.Data;
using reactBackend.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace reactBackend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DiagnosticController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<DiagnosticController> _logger;
        
        public DiagnosticController(
            IConfiguration configuration, 
            ApplicationDbContext context, 
            ILogger<DiagnosticController> logger)
        {
            _configuration = configuration;
            _context = context;
            _logger = logger;
        }
        
        [HttpGet("simple-test")]
        public IActionResult SimpleTest()
        {
            try
            {
                _logger.LogInformation("تم الوصول إلى نقطة نهاية الاختبار البسيط");
                // إرجاع بيانات ثابتة بدون الوصول إلى قاعدة البيانات
                return Ok(new { 
                    message = "تم الوصول إلى نقطة النهاية بنجاح بدون الوصول لقاعدة البيانات",
                    timestamp = DateTime.UtcNow 
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في اختبار بسيط");
                return StatusCode(500, new { 
                    error = ex.Message,
                    stackTrace = ex.StackTrace,
                    innerError = ex.InnerException?.Message
                });
            }
        }
        
        [HttpGet("db-connection")]
        public async Task<IActionResult> TestDbConnection()
        {
            try
            {
                _logger.LogInformation("اختبار الاتصال بقاعدة البيانات");
                var result = new Dictionary<string, object>();
                
                // اختبار سلسلة الاتصال
                var connectionString = _configuration.GetConnectionString("DefaultConnection");
                result.Add("ConnectionStringFirstChars", connectionString?.Substring(0, Math.Min(30, connectionString?.Length ?? 0)));
                
                // محاولة الاتصال
                bool canConnect = await _context.Database.CanConnectAsync();
                result.Add("CanConnect", canConnect);
                
                if (canConnect)
                {
                    // اختبار استرجاع عدد المستخدمين
                    try
                    {
                        int userCount = await _context.Users.CountAsync();
                        result.Add("UserCount", userCount);
                    }
                    catch (Exception ex)
                    {
                        result.Add("UserQueryError", ex.Message);
                    }
                }
                
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في اختبار الاتصال");
                return StatusCode(500, new { 
                    error = ex.Message,
                    stackTrace = ex.StackTrace,
                    innerError = ex.InnerException?.Message
                });
            }
        }
        
        [HttpGet("simple-query")]
        public async Task<IActionResult> SimpleQuery()
        {
            try
            {
                _logger.LogInformation("تنفيذ استعلام بسيط");
                
                // محاولة استرجاع عدد المستخدمين فقط
                var userCount = await _context.Users.CountAsync();
                
                // محاولة استرجاع أسماء الأدوار
                var roles = await _context.Roles.Select(r => r.Name).ToListAsync();
                
                return Ok(new { 
                    userCount,
                    roles
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في استعلام بسيط");
                return StatusCode(500, new { 
                    error = ex.Message,
                    stackTrace = ex.StackTrace,
                    innerError = ex.InnerException?.Message 
                });
            }
        }
        
        [HttpGet("check-environment")]
        public IActionResult CheckEnvironment()
        {
            try
            {
                _logger.LogInformation("التحقق من بيئة التشغيل");
                
                var envInfo = new Dictionary<string, object>
                {
                    ["AspNetCoreEnvironment"] = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "غير محدد",
                    ["OSDescription"] = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
                    ["FrameworkDescription"] = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
                    ["ContentRootPath"] = HttpContext.RequestServices.GetService<IWebHostEnvironment>()?.ContentRootPath ?? "غير متوفر",
                    ["CurrentDirectory"] = Environment.CurrentDirectory
                };
                
                // فحص وجود المجلدات الهامة
                var webHostEnv = HttpContext.RequestServices.GetService<IWebHostEnvironment>();
                if (webHostEnv != null)
                {
                    string wwwrootPath = Path.Combine(webHostEnv.ContentRootPath, "wwwroot");
                    envInfo["WwwrootExists"] = Directory.Exists(wwwrootPath);
                    
                    if (Directory.Exists(wwwrootPath))
                    {
                        string imagesPath = Path.Combine(wwwrootPath, "images");
                        string invoicesPath = Path.Combine(wwwrootPath, "invoices");
                        
                        envInfo["ImagesDirectoryExists"] = Directory.Exists(imagesPath);
                        envInfo["InvoicesDirectoryExists"] = Directory.Exists(invoicesPath);
                    }
                }
                
                return Ok(envInfo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في التحقق من بيئة التشغيل");
                return StatusCode(500, new { 
                    error = ex.Message,
                    stackTrace = ex.StackTrace,
                    innerError = ex.InnerException?.Message 
                });
            }
        }
    }
}