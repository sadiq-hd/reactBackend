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
using System.Security.Cryptography;

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
        private readonly IOtpService _otpService;

        public AuthController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            RoleManager<IdentityRole> roleManager,
            IConfiguration configuration,
            IOtpService otpService)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _roleManager = roleManager;
            _configuration = configuration;
            _otpService = otpService;
        }

        private async Task<string> CreateToken(ApplicationUser user)
        {
            var userRoles = await _userManager.GetRolesAsync(user);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Email, user.Email ?? string.Empty),
                new Claim(ClaimTypes.Name, user.UserName ?? string.Empty)
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
                return BadRequest("البريد الإلكتروني أو رقم الهاتف أو كلمة المرور غير صحيحة");

            var result = await _signInManager.CheckPasswordSignInAsync(user, loginDto.Password, false);

            if (!result.Succeeded)
                return BadRequest("البريد الإلكتروني أو رقم الهاتف أو كلمة المرور غير صحيحة");

            // إرسال رمز OTP للتحقق ثنائي العامل
            // var otp = _otpService.GenerateOtp();
            // if (!string.IsNullOrEmpty(user.PhoneNumber))
            // {
            //     _otpService.StoreOtp(user.PhoneNumber, otp);
            // }
            // else
            // {
            //     return BadRequest("رقم الهاتف غير متوفر للمستخدم");
            // }

            // // في بيئة الإنتاج سيتم إرسال الرمز عبر SMS
            // // هنا نقوم بإرجاع الرمز للاختبار فقط
            // var isDevelopment = _configuration["ASPNETCORE_ENVIRONMENT"] == "Development";

            // if (isDevelopment)
            // {
            //     return Ok(new
            //     {
            //         requireOtp = true,
            //         phoneNumber = user.PhoneNumber,
            //         message = "تم إرسال رمز التحقق إلى هاتفك المحمول",
            //         testOtp = otp  // في بيئة الإنتاج يجب إزالة هذا
            //     });
            // }
            // else
            // {
            //     return Ok(new
            //     {
            //         requireOtp = true,
            //         phoneNumber = user.PhoneNumber,
            //         message = "تم إرسال رمز التحقق إلى هاتفك المحمول"
            //     });
            // }
            var otp = _otpService.GenerateOtp();
    if (!string.IsNullOrEmpty(user.PhoneNumber))
    {
        _otpService.StoreOtp(user.PhoneNumber, otp);
    }
    else
    {
        return BadRequest("رقم الهاتف غير متوفر للمستخدم");
    }

    // إرجاع الرمز دائماً بغض النظر عن البيئة
    return Ok(new
    {
        requireOtp = true,
        phoneNumber = user.PhoneNumber,
        message = "تم إرسال رمز التحقق إلى هاتفك المحمول",
        otp = otp  // إضافة الرمز للاستجابة
    });

        }

        [HttpPost("verify-otp")]
        public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpDto model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var user = await _userManager.Users.FirstOrDefaultAsync(u => u.PhoneNumber == model.PhoneNumber);
            
            if (user == null)
                return BadRequest("المستخدم غير موجود");

            // التحقق من صحة رمز OTP
            bool isValidOtp = _otpService.VerifyOtp(model.PhoneNumber, model.Otp);
            
            if (!isValidOtp)
                return BadRequest("رمز التحقق غير صحيح أو منتهي الصلاحية");

            // حذف رمز OTP بعد التحقق الناجح
            if (!string.IsNullOrEmpty(model.PhoneNumber))
            {
                _otpService.RemoveOtp(model.PhoneNumber);
            }

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

            if (await _userManager.Users.AnyAsync(u => u.PhoneNumber == registerDto.PhoneNumber))
                return BadRequest("رقم الهاتف مستخدم بالفعل");

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

            // إرسال رمز OTP للتحقق من رقم الهاتف
            var otp = _otpService.GenerateOtp();
            if (!string.IsNullOrEmpty(user.PhoneNumber))
            {
                _otpService.StoreOtp(user.PhoneNumber, otp);
            }
            else
            {
                return BadRequest("رقم الهاتف غير متوفر للمستخدم");
            }

            // في بيئة الإنتاج سيتم إرسال الرمز عبر SMS
            var isDevelopment = _configuration["ASPNETCORE_ENVIRONMENT"] == "Development";

            if (isDevelopment)
            {
                return Ok(new
                {
                    message = "تم إنشاء الحساب بنجاح. الرجاء التحقق من رقم هاتفك",
                    requirePhoneVerification = true,
                    phoneNumber = user.PhoneNumber,
                    testOtp = otp  // في بيئة الإنتاج يجب إزالة هذا
                });
            }
            else
            {
                return Ok(new
                {
                    message = "تم إنشاء الحساب بنجاح. الرجاء التحقق من رقم هاتفك",
                    requirePhoneVerification = true,
                    phoneNumber = user.PhoneNumber,
                     testOtp = otp  // في بيئة الإنتاج يجب إزالة هذا

                });
            }
    //           var otp = _otpService.GenerateOtp();
    // if (!string.IsNullOrEmpty(user.PhoneNumber))
    // {
    //     _otpService.StoreOtp(user.PhoneNumber, otp);
    // }
    // else
    // {
    //     return BadRequest("رقم الهاتف غير متوفر للمستخدم");
    // }

    // // إرجاع الرمز دائماً بغض النظر عن البيئة
    // return Ok(new
    // {
    //     message = "تم إنشاء الحساب بنجاح. الرجاء التحقق من رقم هاتفك",
    //     requirePhoneVerification = true,
    //     phoneNumber = user.PhoneNumber,
    //     otp = otp  // إضافة الرمز للاستجابة
    // });

        }

        [HttpPost("verify-phone")]
        public async Task<IActionResult> VerifyPhone([FromBody] VerifyOtpDto model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var user = await _userManager.Users.FirstOrDefaultAsync(u => u.PhoneNumber == model.PhoneNumber);
            
            if (user == null)
                return BadRequest("المستخدم غير موجود");

            // التحقق من صحة رمز OTP
            bool isValidOtp = _otpService.VerifyOtp(model.PhoneNumber, model.Otp);
            
            if (!isValidOtp)
                return BadRequest("رمز التحقق غير صحيح أو منتهي الصلاحية");

            // تحديث حالة تأكيد رقم الهاتف
            user.PhoneNumberConfirmed = true;
            await _userManager.UpdateAsync(user);

            // حذف رمز OTP بعد التحقق الناجح
            _otpService.RemoveOtp(model.PhoneNumber);

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

        [HttpPost("resend-otp")]
        public async Task<IActionResult> ResendOtp([FromBody] ResendOtpDto model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var user = await _userManager.Users.FirstOrDefaultAsync(u => u.PhoneNumber == model.PhoneNumber);
            
            if (user == null)
                return BadRequest("المستخدم غير موجود");

            // إرسال رمز OTP جديد
            var otp = _otpService.GenerateOtp();
            _otpService.StoreOtp(user.PhoneNumber, otp);

            // في بيئة الإنتاج سيتم إرسال الرمز عبر SMS
            var isDevelopment = _configuration["ASPNETCORE_ENVIRONMENT"] == "Development";

            if (isDevelopment)
            {
                return Ok(new
                {
                    message = "تم إرسال رمز جديد إلى هاتفك المحمول",
                    testOtp = otp  // في بيئة الإنتاج يجب إزالة هذا
                });
            }
            else
            {
                return Ok(new
                {
                    message = "تم إرسال رمز جديد إلى هاتفك المحمول",
                    testOtp = otp 
                });
            }
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
                // إرجاع رسالة نجاح حتى لو كان المستخدم غير موجود (لأسباب أمنية)
                return Ok(new { message = "إذا كان البريد الإلكتروني مسجلاً، سيتم إرسال رمز التحقق إلى هاتفك المحمول" });

            // إرسال رمز OTP للتحقق وإعادة تعيين كلمة المرور
            var otp = _otpService.GenerateOtp();
            if (!string.IsNullOrEmpty(user.PhoneNumber))
            {
                _otpService.StoreOtp(user.PhoneNumber, otp, "ResetPassword");
            }
            else
            {
                // بدلاً من إرجاع خطأ، نحتفظ بالرسالة العامة لأسباب أمنية
                return Ok(new { message = "إذا كان البريد الإلكتروني مسجلاً، سيتم إرسال رمز التحقق إلى هاتفك المحمول" });
            }

            // في بيئة الإنتاج سيتم إرسال الرمز عبر SMS
            var isDevelopment = _configuration["ASPNETCORE_ENVIRONMENT"] == "Development";

            if (isDevelopment)
            {
                return Ok(new
                {
                    message = "تم إرسال رمز التحقق إلى هاتفك المحمول",
                    phoneNumber = user.PhoneNumber,
                    testOtp = otp  // في بيئة الإنتاج يجب إزالة هذا
                });
            }
            else
            {
                return Ok(new
                {
                    message = "تم إرسال رمز التحقق إلى هاتفك المحمول",
                    phoneNumber = user.PhoneNumber,
                    testOtp = otp  // في بيئة الإنتاج يجب إزالة هذا

                });
            }
        }

        [HttpPost("verify-reset-otp")]
        public async Task<IActionResult> VerifyResetOtp([FromBody] VerifyOtpDto model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var user = await _userManager.Users.FirstOrDefaultAsync(u => u.PhoneNumber == model.PhoneNumber);
            
            if (user == null)
                return BadRequest("المستخدم غير موجود");

            // التحقق من صحة رمز OTP
            bool isValidOtp = _otpService.VerifyOtp(model.PhoneNumber, model.Otp, "ResetPassword");
            
            if (!isValidOtp)
                return BadRequest("رمز التحقق غير صحيح أو منتهي الصلاحية");

            // إنشاء رمز إعادة تعيين كلمة المرور مؤقت
            var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);

            return Ok(new
            {
                message = "تم التحقق بنجاح. يمكنك الآن إعادة تعيين كلمة المرور",
                email = user.Email,
                token = resetToken
            });
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
                return BadRequest(new { message = "فشلت عملية إعادة تعيين كلمة المرور" });

            // تنفيذ إعادة تعيين كلمة المرور
            var result = await _userManager.ResetPasswordAsync(user, model.Token, model.NewPassword);

            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                return BadRequest(new { message = "فشلت عملية إعادة تعيين كلمة المرور", errors });
            }

            // تحديث الطابع الأمني
            await _userManager.UpdateSecurityStampAsync(user);

            // حذف رمز OTP بعد إعادة تعيين كلمة المرور بنجاح
            if (!string.IsNullOrEmpty(user.PhoneNumber))
            {
                _otpService.RemoveOtp(user.PhoneNumber, "ResetPassword");
            }

            return Ok(new { message = "تم إعادة تعيين كلمة المرور بنجاح" });
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

                // التحقق من رقم الهاتف
                if (user.PhoneNumber != model.PhoneNumber && !string.IsNullOrEmpty(model.PhoneNumber))
                {
                    var existingPhone = await _userManager.Users.AnyAsync(u => 
                        u.PhoneNumber == model.PhoneNumber && u.Id != userId);
                        
                    if (existingPhone)
                        return BadRequest("رقم الهاتف مستخدم بالفعل");
                        
                    // إذا تم تغيير رقم الهاتف، نحتاج إلى تأكيده مرة أخرى
                    if (model.VerifyNewPhone)
                    {
                        // إرسال رمز OTP للتحقق من رقم الهاتف الجديد
                        var otp = _otpService.GenerateOtp();
                        if (!string.IsNullOrEmpty(model.PhoneNumber))
                        {
                            _otpService.StoreOtp(model.PhoneNumber, otp, "UpdatePhone");
                        }
                        else
                        {
                            return BadRequest("رقم الهاتف غير صالح");
                        }
                        
                        // حفظ رقم الهاتف الجديد في متغير مؤقت لاستخدامه عند التحقق
                        user.NewPhoneNumber = model.PhoneNumber;
                        await _userManager.UpdateAsync(user);
                        
                        var isDevelopment = _configuration["ASPNETCORE_ENVIRONMENT"] == "Development";
                        if (isDevelopment)
                        {
                            return Ok(new
                            {
                                requirePhoneVerification = true,
                                phoneNumber = model.PhoneNumber,
                                message = "الرجاء التحقق من رقم الهاتف الجديد",
                                testOtp = otp
                            });
                        }
                        else
                        {
                            return Ok(new
                            {
                                 requirePhoneVerification = true,
                                phoneNumber = model.PhoneNumber,
                                message = "الرجاء التحقق من رقم الهاتف الجديد",
                                testOtp = otp
                            });
                        }
                    }
                }

                // تحديث المعلومات
                user.Name = model.Name;
                user.Email = model.Email;
                user.UserName = model.Email;
                // سيتم تحديث رقم الهاتف بعد التحقق منه إذا تم تغييره

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

        [HttpPost("verify-new-phone")]
        [Authorize]
        public async Task<IActionResult> VerifyNewPhone([FromBody] VerifyOtpDto model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId == null)
                return Unauthorized();

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return NotFound("المستخدم غير موجود");

            // التحقق من صحة رمز OTP
            bool isValidOtp = _otpService.VerifyOtp(model.PhoneNumber, model.Otp, "UpdatePhone");
            
            if (!isValidOtp)
                return BadRequest("رمز التحقق غير صحيح أو منتهي الصلاحية");

            // تحديث رقم الهاتف
            user.PhoneNumber = model.PhoneNumber;
            user.PhoneNumberConfirmed = true;
            user.NewPhoneNumber = string.Empty;
            await _userManager.UpdateAsync(user);

            // حذف رمز OTP بعد التحقق الناجح
            if (!string.IsNullOrEmpty(model.PhoneNumber))
            {
                _otpService.RemoveOtp(model.PhoneNumber, "UpdatePhone");
            }

            return Ok(new
            {
                message = "تم تحديث رقم الهاتف بنجاح",
                id = user.Id,
                email = user.Email,
                userName = user.UserName,
                name = user.Name,
                phoneNumber = user.PhoneNumber,
                roles = await _userManager.GetRolesAsync(user)
            });
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