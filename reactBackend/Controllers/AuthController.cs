using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using reactBackend.Models;
using reactBackend.Dtos;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using reactBackend.Services;


namespace reactBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IConfiguration _configuration;
        private readonly IEmailService _emailService;


        public AuthController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            RoleManager<IdentityRole> roleManager,
            IConfiguration configuration,
            IEmailService emailService)

        {
            _userManager = userManager;
            _signInManager = signInManager;
            _roleManager = roleManager;
            _configuration = configuration;
            _emailService = emailService;

        }

        private async Task<string> CreateToken(ApplicationUser user)
        {
            var userRoles = await _userManager.GetRolesAsync(user);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Name, user.UserName)
            };

            // إضافة الأدوار للـ claims
            foreach (var role in userRoles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
                _configuration.GetSection("JWT:Key").Value!));

            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _configuration.GetSection("JWT:Issuer").Value,
                audience: _configuration.GetSection("JWT:Audience").Value,
                claims: claims,
                expires: DateTime.Now.AddDays(1),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginDto loginDto)
        {
            var user = await _userManager.FindByEmailAsync(loginDto.EmailOrPhone) ??
                      await _userManager.Users.FirstOrDefaultAsync(u => u.PhoneNumber == loginDto.EmailOrPhone);

            if (user == null)
                return BadRequest("البريد الإلكتروني أو كلمة المرور غير صحيحة");

            var result = await _signInManager.CheckPasswordSignInAsync(user, loginDto.Password, false);

            if (!result.Succeeded)
                return BadRequest("البريد الإلكتروني أو كلمة المرور غير صحيحة");

            var userRoles = await _userManager.GetRolesAsync(user);
            var token = await CreateToken(user);

            return Ok(new
            {
                token,
                user = new
                {
                    id = user.Id,
                    email = user.Email,
                    userName = user.UserName,
                    name = user.Name,
                    phoneNumber = user.PhoneNumber,
                    role = userRoles.FirstOrDefault()?.ToLower()
                }
            });
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register(RegisterDto registerDto)
        {
            if (await _userManager.FindByEmailAsync(registerDto.Email) != null)
                return BadRequest("البريد الإلكتروني مستخدم بالفعل");

            var user = new ApplicationUser
            {
                UserName = registerDto.Username,
                Email = registerDto.Email,
                Name = registerDto.Name,
                PhoneNumber = registerDto.PhoneNumber
            };

            var result = await _userManager.CreateAsync(user, registerDto.Password);

            if (!result.Succeeded)
                return BadRequest(result.Errors);

            // إضافة الدور الافتراضي "user"
            await _userManager.AddToRoleAsync(user, "user");

            return Ok(new { message = "تم إنشاء الحساب بنجاح" });
        }

        [HttpPost("forgot-password")]
public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto model)
{
    if (!ModelState.IsValid)
        return BadRequest(ModelState);

    var user = await _userManager.FindByEmailAsync(model.Email);
    if (user == null)
        // إرجاع رسالة نجاح حتى لو كان المستخدم غير موجود (لأسباب أمنية)
        return Ok(new { message = "تم إرسال رابط إعادة تعيين كلمة المرور إلى بريدك الإلكتروني" });

    // إنشاء رمز إعادة تعيين كلمة المرور
    var token = await _userManager.GeneratePasswordResetTokenAsync(user);

    // إنشاء رابط إعادة تعيين كلمة المرور
    var clientUrl = _configuration["ClientURL"] ?? "https://recat-onlinestore.netlify.app";
    var resetUrl = $"{clientUrl}/reset-password?email={Uri.EscapeDataString(model.Email)}&token={Uri.EscapeDataString(token)}";

    // إنشاء محتوى البريد الإلكتروني
    var emailContent = $@"
        <div style='direction: rtl; text-align: right; font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #ddd; border-radius: 5px;'>
            <h2 style='color: #333;'>إعادة تعيين كلمة المرور</h2>
            <p>مرحباً {user.Name}،</p>
            <p>لقد تلقينا طلباً لإعادة تعيين كلمة المرور الخاصة بحسابك. إذا كنت قد طلبت إعادة تعيين كلمة المرور، فيرجى النقر على الرابط أدناه:</p>
            <p style='margin: 20px 0;'>
                <a href='{resetUrl}' style='display: inline-block; padding: 10px 20px; background-color: #4CAF50; color: white; text-decoration: none; border-radius: 4px;'>إعادة تعيين كلمة المرور</a>
            </p>
            <p>أو يمكنك نسخ الرابط التالي ولصقه في متصفحك:</p>
            <p style='background-color: #f5f5f5; padding: 10px; border-radius: 3px; word-break: break-all;'>{resetUrl}</p>
            <p>إذا لم تطلب إعادة تعيين كلمة المرور، يمكنك تجاهل هذا البريد الإلكتروني.</p>
            <p>مع تحيات،<br>فريق المتجر الإلكتروني</p>
        </div>
    ";

    try
    {
        // إرسال البريد الإلكتروني باستخدام خدمة SendGrid
        var emailSent = await _emailService.SendEmailAsync(
            user.Email,
            "إعادة تعيين كلمة المرور - المتجر الإلكتروني",
            emailContent
        );

        if (!emailSent)
        {
            return StatusCode(500, new { message = "فشل في إرسال البريد الإلكتروني، يرجى المحاولة مرة أخرى لاحقاً" });
        }

        // في بيئة التطوير، يمكن إرجاع معلومات إضافية للتسهيل
        var isDevelopment = _configuration["ASPNETCORE_ENVIRONMENT"] == "Development";
        if (isDevelopment)
        {
            return Ok(new
            {
                message = "تم إرسال رابط إعادة تعيين كلمة المرور إلى بريدك الإلكتروني",
                resetUrl = resetUrl,
                token = token
            });
        }

        return Ok(new { message = "تم إرسال رابط إعادة تعيين كلمة المرور إلى بريدك الإلكتروني" });
    }
    catch (Exception ex)
    {
        // سجل الخطأ
        Console.WriteLine($"Error sending email: {ex.Message}");
        return StatusCode(500, new { message = "حدث خطأ أثناء معالجة طلبك" });
    }
}

[HttpPost("reset-password")]
public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto model)
{
    if (!ModelState.IsValid)
        return BadRequest(ModelState);

    var user = await _userManager.FindByEmailAsync(model.Email);
    if (user == null)
        return BadRequest(new { message = "فشلت عملية إعادة تعيين كلمة المرور" });

    // فك تشفير الرمز إذا كان مشفرًا
    var decodedToken = Uri.UnescapeDataString(model.Token);

    // تنفيذ إعادة تعيين كلمة المرور
    var result = await _userManager.ResetPasswordAsync(user, decodedToken, model.NewPassword);

    if (!result.Succeeded)
    {
        var errors = string.Join(", ", result.Errors.Select(e => e.Description));
        return BadRequest(new { message = "فشلت عملية إعادة تعيين كلمة المرور", errors });
    }

    // تحديث الطابع الأمني
    await _userManager.UpdateSecurityStampAsync(user);

    return Ok(new { message = "تم إعادة تعيين كلمة المرور بنجاح" });
}

[HttpGet("validate-reset-token")]
public async Task<IActionResult> ValidateResetToken([FromQuery] string email, [FromQuery] string token)
{
    if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(token))
        return BadRequest(new { isValid = false, message = "البريد الإلكتروني والرمز مطلوبان" });

    var user = await _userManager.FindByEmailAsync(email);
    if (user == null)
        return Ok(new { isValid = false });

    // التحقق من صحة الرمز
    var purpose = "ResetPassword";
    var isTokenValid = await _userManager.VerifyUserTokenAsync(
        user,
        _userManager.Options.Tokens.PasswordResetTokenProvider,
        purpose,
        token
    );

    return Ok(new { isValid = isTokenValid });
}

        [HttpGet]
        [Authorize(Roles = "admin")]
        public async Task<ActionResult<IEnumerable<object>>> GetAllUsers()
        {
            var users = await _userManager.Users
                .Select(u => new
                {
                    u.Id,
                    u.UserName,
                    u.Email,
                    u.Name,
                    u.PhoneNumber
                })
                .ToListAsync();
            return Ok(users);
        }

        [HttpGet("current")]
        [Authorize]
        public async Task<ActionResult<object>> GetCurrentUser()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId == null)
                return Unauthorized();

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return NotFound();

            return Ok(new
            {
                id = user.Id,
                userName = user.UserName,
                email = user.Email,
                name = user.Name,
                phoneNumber = user.PhoneNumber,
                roles = await _userManager.GetRolesAsync(user)
            });
        }




        [HttpGet("stats")]
        [Authorize(Roles = "admin")]
        public async Task<ActionResult> GetUserStats()
        {
            var totalUsers = await _userManager.Users.CountAsync();
            var adminCount = (await _userManager.GetUsersInRoleAsync("admin")).Count;
            var regularUserCount = (await _userManager.GetUsersInRoleAsync("user")).Count;

            return Ok(new
            {
                TotalUsers = totalUsers,
                AdminCount = adminCount,
                RegularUserCount = regularUserCount
            });
        }


        [HttpPost("check-email")]
        [Authorize]
        public async Task<IActionResult> CheckEmail([FromBody] CheckEmailDto model)
        {
            if (model == null || string.IsNullOrEmpty(model.Email))
                return BadRequest("البريد الإلكتروني مطلوب");

            var existingUser = await _userManager.FindByEmailAsync(model.Email);
            var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            // التحقق مما إذا كان البريد الإلكتروني موجودًا ولكن ليس للمستخدم الحالي
            bool exists = existingUser != null && existingUser.Id != currentUserId;

            return Ok(new { exists });
        }

        [HttpPut("update-profile")]
        [Authorize]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileDto model)
        {
            try
            {
                if (model == null || string.IsNullOrEmpty(model.Name) || string.IsNullOrEmpty(model.Email))
                    return BadRequest("بيانات غير صالحة");

                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (userId == null)
                    return Unauthorized();

                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                    return NotFound("المستخدم غير موجود");

                // التحقق من البريد الإلكتروني
                if (user.Email != model.Email)
                {
                    var existingUser = await _userManager.FindByEmailAsync(model.Email);
                    if (existingUser != null && existingUser.Id != userId)
                        return BadRequest("البريد الإلكتروني مستخدم بالفعل");
                }

                // تحديث المعلومات
                user.Name = model.Name;
                user.Email = model.Email;
                user.UserName = model.Email;
                user.PhoneNumber = model.PhoneNumber ?? user.PhoneNumber;

                var result = await _userManager.UpdateAsync(user);
                if (!result.Succeeded)
                {
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    Console.WriteLine($"Update failed: {errors}");
                    return BadRequest(result.Errors);
                }

                return Ok(new
                {
                    id = user.Id,
                    email = user.Email,
                    userName = user.UserName,
                    name = user.Name,
                    phoneNumber = user.PhoneNumber,
                    roles = await _userManager.GetRolesAsync(user)
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return StatusCode(500, new { error = "An error occurred", message = ex.Message });
            }
        }

        [HttpPost("change-password")]
        [Authorize]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId == null)
                return Unauthorized();

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return NotFound("المستخدم غير موجود");

            // التحقق من صحة كلمة المرور الحالية
            var isPasswordCorrect = await _userManager.CheckPasswordAsync(user, model.CurrentPassword);
            if (!isPasswordCorrect)
                return BadRequest("كلمة المرور الحالية غير صحيحة");

            // تغيير كلمة المرور
            var result = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
            if (!result.Succeeded)
                return BadRequest(result.Errors);

            // إعادة تسجيل الدخول (اختياري)
            await _signInManager.RefreshSignInAsync(user);

            return Ok(new { message = "تم تغيير كلمة المرور بنجاح" });
        }
    }
}