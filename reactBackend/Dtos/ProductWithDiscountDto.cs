using System.Collections.Generic;

namespace reactBackend.Dtos
{
    public class ProductWithDiscountDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int Stock { get; set; }
        public string Category { get; set; } = string.Empty;
        public List<string>? Images { get; set; }

        // معلومات الخصم
        public bool HasDiscount { get; set; }
        public decimal? DiscountedPrice { get; set; }
        public string? DiscountName { get; set; }
        public decimal? DiscountValue { get; set; }
        public string? DiscountType { get; set; }

        // معلومات التقييم (الإضافة الجديدة)
        public decimal? AverageRating { get; set; }
        public int ReviewsCount { get; set; }
        public int CommentsCount { get; set; }
    }
}