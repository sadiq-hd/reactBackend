using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace reactBackend.Models
{
    public class ApplicationUser : IdentityUser
    {
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

  [MaxLength(10)]
        public string? NewPhoneNumber { get; set; }
        // العلاقات
        public virtual ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();
        public virtual ICollection<WishlistItem> WishlistItems { get; set; } = new List<WishlistItem>();
        public virtual ICollection<Order> Orders { get; set; } = new List<Order>();
    }
}