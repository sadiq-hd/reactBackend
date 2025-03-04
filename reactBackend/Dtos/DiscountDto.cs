// Dtos/DiscountDto.cs
public class CreateDiscountDto
{
    public string Name { get; set; }
    public string Description { get; set; }
    public DiscountType Type { get; set; }
    public decimal Value { get; set; }
    public DiscountScope Scope { get; set; }
    public string? CategoryName { get; set; }
    public List<int>? ProductIds { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsActive { get; set; }
}

public class DiscountResponseDto
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public string Type { get; set; }
    public decimal Value { get; set; }
    public string Scope { get; set; }
    public string? CategoryName { get; set; }
    public List<ProductResponseDto>? Products { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ProductResponseDto
{
    public int Id { get; set; }
    public string Name { get; set; }
}

public class ProductWithDiscountDto
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public string Category { get; set; }
    public decimal Price { get; set; }
    public decimal? DiscountedPrice { get; set; }
    public int Stock { get; set; }
    public List<string> Images { get; set; }
    public bool HasDiscount { get; set; }
    public string DiscountName { get; set; }
    public decimal? DiscountValue { get; set; }
    public string DiscountType { get; set; }
}