using System.ComponentModel.DataAnnotations;

namespace reactBackend.Dtos
{
    // دالة طلب إنشاء تعليق جديد
    public class CreateCommentDto
    {
        [Required(ErrorMessage = "معرف المنتج مطلوب")]
        public int ProductId { get; set; }

        [Required(ErrorMessage = "محتوى التعليق مطلوب")]
        [StringLength(1000, ErrorMessage = "يجب أن يكون طول التعليق أقل من 1000 حرف")]
        public string Content { get; set; } = string.Empty;

        public int? ParentCommentId { get; set; }
    }

    // دالة طلب تحديث تعليق
    public class UpdateCommentDto
    {
        [Required(ErrorMessage = "محتوى التعليق مطلوب")]
        [StringLength(1000, ErrorMessage = "يجب أن يكون طول التعليق أقل من 1000 حرف")]
        public string Content { get; set; } = string.Empty;
    }

    // دالة استجابة التعليق
    public class CommentResponseDto
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public int LikesCount { get; set; }
        public bool IsLikedByCurrentUser { get; set; }
        public int? ParentCommentId { get; set; }
        public List<CommentResponseDto>? Replies { get; set; }
    }

    // دالة طلب إنشاء تقييم جديد
    public class CreateReviewDto
    {
        [Required(ErrorMessage = "معرف المنتج مطلوب")]
        public int ProductId { get; set; }

        [Required(ErrorMessage = "التقييم مطلوب")]
        [Range(1, 5, ErrorMessage = "يجب أن يكون التقييم بين 1 و 5")]
        public int Rating { get; set; }

        [StringLength(1000, ErrorMessage = "يجب أن يكون طول التعليق أقل من 1000 حرف")]
        public string? Comment { get; set; }
    }

    // دالة طلب تحديث تقييم
    public class UpdateReviewDto
    {
        [Required(ErrorMessage = "التقييم مطلوب")]
        [Range(1, 5, ErrorMessage = "يجب أن يكون التقييم بين 1 و 5")]
        public int Rating { get; set; }

        [StringLength(1000, ErrorMessage = "يجب أن يكون طول التعليق أقل من 1000 حرف")]
        public string? Comment { get; set; }
    }

    // دالة استجابة التقييم
    public class ReviewResponseDto
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public int Rating { get; set; }
        public string? Comment { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public bool IsVerifiedPurchase { get; set; }

        // إضافة حقول جديدة لمعلومات المنتج
        public string? ProductName { get; set; }
        public string? ProductImage { get; set; }
    }

    // دالة ملخص التقييمات
    public class ReviewSummaryDto
    {
        public int ProductId { get; set; }
        public double AverageRating { get; set; }
        public int TotalReviews { get; set; }
        public Dictionary<int, int> RatingDistribution { get; set; } = new Dictionary<int, int>();
    }
}