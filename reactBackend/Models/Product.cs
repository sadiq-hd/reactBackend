using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace reactBackend.Models
{
    public class Product
    {
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string Category { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "السعر يجب أن يكون أكبر من صفر")]
        public decimal Price { get; set; }

        [Required]
        [Range(0, int.MaxValue)]
        public int Stock { get; set; }

        [Required]
        [StringLength(1000)]
        public string Description { get; set; } = string.Empty;

        // تعديل نوع البيانات ليكون متوافق
        public decimal? AverageRating { get; set; }
        public int TotalReviews { get; set; } = 0;

        public virtual ICollection<ProductImage> Images { get; set; } = new List<ProductImage>();

        [JsonIgnore]
        public virtual ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();

        [JsonIgnore]
        public virtual ICollection<WishlistItem> WishlistItems { get; set; } = new List<WishlistItem>();

        [JsonIgnore]
        public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();

        // إضافة مجموعات جديدة للتعليقات والتقييمات
        [JsonIgnore]
        public virtual ICollection<ProductComment> Comments { get; set; } = new List<ProductComment>();

        [JsonIgnore]
        public virtual ICollection<ProductReview> Reviews { get; set; } = new List<ProductReview>();
    }
}