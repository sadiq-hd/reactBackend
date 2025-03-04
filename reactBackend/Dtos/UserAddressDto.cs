using System.ComponentModel.DataAnnotations;

namespace reactBackend.Dtos
{
    // DTO لإنشاء/تحديث عنوان مستخدم
    public class UserAddressDto
    {
        [Required(ErrorMessage = "الاسم الكامل مطلوب")]
        [StringLength(100, ErrorMessage = "الاسم الكامل يجب ألا يتجاوز 100 حرف")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "رقم الهاتف مطلوب")]
        [RegularExpression(@"^05\d{8}$", ErrorMessage = "رقم الهاتف غير صحيح، يجب أن يبدأ ب 05 ويتكون من 10 أرقام")]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "المدينة مطلوبة")]
        [StringLength(50, ErrorMessage = "اسم المدينة يجب ألا يتجاوز 50 حرف")]
        public string City { get; set; } = string.Empty;

        [Required(ErrorMessage = "الشارع مطلوب")]
        [StringLength(100, ErrorMessage = "اسم الشارع يجب ألا يتجاوز 100 حرف")]
        public string Street { get; set; } = string.Empty;

        [StringLength(20, ErrorMessage = "رقم المبنى يجب ألا يتجاوز 20 حرف")]
        public string? BuildingNumber { get; set; }

        [StringLength(200, ErrorMessage = "التفاصيل الإضافية يجب ألا تتجاوز 200 حرف")]
        public string? AdditionalDetails { get; set; }

        public bool IsDefault { get; set; } = false;
    }

    // DTO لاستجابة استعلام عنوان مستخدم
    public class UserAddressResponseDto
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string Street { get; set; } = string.Empty;
        public string? BuildingNumber { get; set; }
        public string? AdditionalDetails { get; set; }
        public bool IsDefault { get; set; } = false;
        public DateTime CreatedAt { get; set; }
    }
}