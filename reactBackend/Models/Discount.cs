// Models/Discount.cs
using reactBackend.Models;

public enum DiscountType
{
    Percentage, // نسبة مئوية
    FixedAmount // مبلغ ثابت
}

public enum DiscountScope
{
    AllProducts, // كل المنتجات
    Category,    // فئة محددة
    Product ,   // منتج محدد
    Global

}

public class Discount
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public DiscountType Type { get; set; }
    public decimal Value { get; set; } // نسبة مئوية أو مبلغ ثابت
    public DiscountScope Scope { get; set; }
    public string? CategoryName { get; set; } // يستخدم إذا كان النطاق هو فئة
    public List<DiscountProduct>? Products { get; set; } // يستخدم إذا كان النطاق هو منتجات محددة
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class DiscountProduct
{
    public int Id { get; set; }
    public int DiscountId { get; set; }
    public Discount Discount { get; set; }
    public int ProductId { get; set; }
    public Product Product { get; set; }
}