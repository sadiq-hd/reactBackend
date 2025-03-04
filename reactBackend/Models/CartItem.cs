using System.ComponentModel.DataAnnotations;

namespace reactBackend.Models
{
    public class CartItem
    {
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        [Required]
        public int ProductId { get; set; }

        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "الكمية يجب أن تكون أكبر من صفر")]
        public int Quantity { get; set; }

        public virtual Product? Product { get; set; }
        public virtual ApplicationUser? User { get; set; } 
    }
}