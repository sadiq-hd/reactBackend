using System.ComponentModel.DataAnnotations;

namespace reactBackend.Dtos
{
    public class UpdateOrderStatusDto
    {
        [Required(ErrorMessage = "حالة الطلب مطلوبة")]
        public string Status { get; set; } = string.Empty;
    }

   
}