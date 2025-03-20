using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace reactBackend.Models
{
    public class ProductComment
    {
        public int Id { get; set; }

        [Required]
        public int ProductId { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        [Required]
        [StringLength(1000)]
        public string Content { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public bool IsDeleted { get; set; } = false;
        public int LikesCount { get; set; } = 0;
        public int? ParentCommentId { get; set; }

        // إضافة حقل الحالة
        public CommentStatus Status { get; set; } = CommentStatus.Approved;

        [ForeignKey("ProductId")]
        [JsonIgnore]
        public virtual Product? Product { get; set; }

        [ForeignKey("UserId")]
        [JsonIgnore]
        public virtual ApplicationUser? User { get; set; }

        [ForeignKey("ParentCommentId")]
        [JsonIgnore]
        public virtual ProductComment? ParentComment { get; set; }

        [JsonIgnore]
        public virtual ICollection<ProductComment>? Replies { get; set; }

        [JsonIgnore]
        public virtual ICollection<CommentLike>? Likes { get; set; }
    }

    public enum CommentStatus
    {
        Pending,
        Approved,
        Rejected
    }
}