using System.ComponentModel.DataAnnotations;

namespace reactBackend.Dtos
{
    public class RegisterDto
    {
        [Required(ErrorMessage = "اسم المستخدم مطلوب")]
        [StringLength(50, MinimumLength = 3, ErrorMessage = "يجب أن يكون اسم المستخدم بين 3 و 50 حرف")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "البريد الإلكتروني مطلوب")]
        [EmailAddress(ErrorMessage = "البريد الإلكتروني غير صالح")]
        [StringLength(100)]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "كلمة المرور مطلوبة")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "يجب أن تكون كلمة المرور 6 أحرف على الأقل")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "الاسم مطلوب")]
        [StringLength(100, ErrorMessage = "الاسم يجب أن لا يتجاوز 100 حرف")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "رقم الهاتف مطلوب")]
        [RegularExpression(@"^05\d{8}$", ErrorMessage = "رقم الجوال غير صالح، يجب أن يبدأ بـ 05 ويتكون من 10 أرقام")]
        public string PhoneNumber { get; set; } = string.Empty;
    }
}