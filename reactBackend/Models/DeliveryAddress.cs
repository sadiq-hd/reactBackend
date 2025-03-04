using System.ComponentModel.DataAnnotations;

namespace reactBackend.Models
{
    public class DeliveryAddress
    {
        public int Id { get; set; }

        [Required]
        public int OrderId { get; set; }

        [Required]
        [StringLength(100)]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [RegularExpression(@"^05\d{8}$", ErrorMessage = "رقم الجوال غير صحيح")]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string City { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string Street { get; set; } = string.Empty;

        [StringLength(20)]
        public string? BuildingNumber { get; set; }

        [StringLength(200)]
        public string? AdditionalDetails { get; set; }

        public virtual Order? Order { get; set; }
    }
}