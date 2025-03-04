using System.ComponentModel.DataAnnotations;

public class ProductDTO
{
    [Required(ErrorMessage = "التصنيف مطلوب")]
    [StringLength(50)]
    public string Category { get; set; } = string.Empty;

    [Required(ErrorMessage = "اسم المنتج مطلوب")]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "السعر مطلوب")]
    [Range(0.01, double.MaxValue, ErrorMessage = "السعر يجب أن يكون أكبر من صفر")]
    public decimal Price { get; set; }

    [Required(ErrorMessage = "الكمية مطلوبة")]
    [Range(0, int.MaxValue, ErrorMessage = "الكمية يجب أن تكون صفر أو أكثر")]
    public int Stock { get; set; }

    [Required(ErrorMessage = "الصور مطلوبة")]
    public List<IFormFile> Images { get; set; } = new();

    [Required(ErrorMessage = "الوصف مطلوب")]
    [StringLength(1000)]
    public string Description { get; set; } = string.Empty;
}