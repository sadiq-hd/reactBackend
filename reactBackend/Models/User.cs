using System.ComponentModel.DataAnnotations;

namespace reactBackend.Models
{
    public class User
    {
        public string Id { get; set; }

        [Required]
        [MaxLength(50)]
        public required string Username { get; set; }

        [Required]
        [EmailAddress]
        [MaxLength(100)]
        public required string Email { get; set; }

        [Required]
        public required string Password { get; set; }

        [Required]
        [MaxLength(100)]
        public string? Name { get; set; }

        [Required]
        [MaxLength(20)]
        public string? Role { get; set; }

        [Required]
        [MaxLength(10)]
        public required string PhoneNumber { get; set; }

        // Navigation Properties
        public virtual ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();
        public virtual ICollection<WishlistItem> WishlistItems { get; set; } = new List<WishlistItem>();
        public virtual ICollection<Order> Orders { get; set; } = new List<Order>();
    }

}