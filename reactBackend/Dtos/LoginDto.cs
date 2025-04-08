using System.ComponentModel.DataAnnotations;

namespace reactBackend.Dtos
{
    public class LoginDto
    {
        [Required(ErrorMessage = "البريد الإلكتروني أو رقم الهاتف مطلوب")]
        public  string EmailOrPhone { get; set; }

        [Required(ErrorMessage = "كلمة المرور مطلوبة")]
        public  string Password { get; set; }
    }


}
