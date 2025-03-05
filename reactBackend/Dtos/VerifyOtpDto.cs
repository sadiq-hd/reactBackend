using System.ComponentModel.DataAnnotations;

namespace reactBackend.Dtos
{
    public class VerifyOtpDto
    {
        [Required(ErrorMessage = "رقم الهاتف مطلوب")]
        [RegularExpression(@"^05\d{8}$", ErrorMessage = "رقم الجوال غير صالح، يجب أن يبدأ بـ 05 ويتكون من 10 أرقام")]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "رمز التحقق مطلوب")]
        [StringLength(6, MinimumLength = 6, ErrorMessage = "رمز التحقق يجب أن يتكون من 6 أرقام")]
        public string Otp { get; set; } = string.Empty;
    }

    public class ResendOtpDto
    {
        [Required(ErrorMessage = "رقم الهاتف مطلوب")]
        [RegularExpression(@"^05\d{8}$", ErrorMessage = "رقم الجوال غير صالح، يجب أن يبدأ بـ 05 ويتكون من 10 أرقام")]
        public string PhoneNumber { get; set; } = string.Empty;
    }

    public class UpdateProfileDto
    {
        [Required(ErrorMessage = "البريد الإلكتروني مطلوب")]
        [EmailAddress(ErrorMessage = "البريد الإلكتروني غير صالح")]
        [StringLength(100)]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "الاسم مطلوب")]
        [StringLength(100, ErrorMessage = "الاسم يجب أن لا يتجاوز 100 حرف")]
        public string Name { get; set; } = string.Empty;

        [RegularExpression(@"^05\d{8}$", ErrorMessage = "رقم الجوال غير صالح، يجب أن يبدأ بـ 05 ويتكون من 10 أرقام")]
        public string PhoneNumber { get; set; } = string.Empty;

        public bool VerifyNewPhone { get; set; } = false;
    }

    public class CheckEmailDto
    {
        [Required(ErrorMessage = "البريد الإلكتروني مطلوب")]
        [EmailAddress(ErrorMessage = "البريد الإلكتروني غير صالح")]
        public string Email { get; set; } = string.Empty;
    }
}