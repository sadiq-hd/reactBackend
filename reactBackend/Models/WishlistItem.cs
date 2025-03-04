using System.ComponentModel.DataAnnotations;

namespace reactBackend.Models
{
    public class WishlistItem
    {
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        [Required]
        public int ProductId { get; set; }

        public DateTime DateAdded { get; set; } = DateTime.UtcNow;

        public virtual Product? Product { get; set; }
        public virtual ApplicationUser? User { get; set; } 
    }
}