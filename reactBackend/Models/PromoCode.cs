// Models/PromoCode.cs
public enum PromoCodeType
{
    Percentage, // نسبة مئوية
    FixedAmount // مبلغ ثابت
}

public class PromoCode
{
    public int Id { get; set; }
    public string Code { get; set; }
    public string Description { get; set; }
    public PromoCodeType Type { get; set; }
    public decimal Value { get; set; }
    public decimal? MinimumOrderAmount { get; set; } // الحد الأدنى للطلب لاستخدام الكود
    public int? MaxUsesTotal { get; set; } // الحد الأقصى لاستخدام الكود (جميع المستخدمين)
    public int? MaxUsesPerUser { get; set; } // الحد الأقصى لاستخدام الكود لكل مستخدم
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<PromoCodeUsage> Usages { get; set; }
}

public class PromoCodeUsage
{
    public int Id { get; set; }
    public int PromoCodeId { get; set; }
    public PromoCode PromoCode { get; set; }
    public string UserId { get; set; }
    public int OrderId { get; set; }
    public Order Order { get; set; }
    public DateTime UsedAt { get; set; } = DateTime.UtcNow;
}