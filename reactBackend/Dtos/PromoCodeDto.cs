// Dtos/PromoCodeDto.cs
public class CreatePromoCodeDto
{
    public string Code { get; set; }
    public string Description { get; set; }
    public PromoCodeType Type { get; set; }
    public decimal Value { get; set; }
    public decimal? MinimumOrderAmount { get; set; }
    public int? MaxUsesTotal { get; set; }
    public int? MaxUsesPerUser { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsActive { get; set; }
}

public class PromoCodeResponseDto
{
    public int Id { get; set; }
    public string Code { get; set; }
    public string Description { get; set; }
    public string Type { get; set; }
    public decimal Value { get; set; }
    public decimal? MinimumOrderAmount { get; set; }
    public int? MaxUsesTotal { get; set; }
    public int? MaxUsesPerUser { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public int UsageCount { get; set; }
}

public class ValidatePromoCodeDto
{
    public string Code { get; set; }
    public decimal OrderTotal { get; set; }
}

public class PromoCodeValidationResult
{
    public bool IsValid { get; set; }
    public string Message { get; set; }
    public decimal? DiscountAmount { get; set; }
    public PromoCodeResponseDto PromoCode { get; set; }
}