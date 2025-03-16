using reactBackend.Models.Enums;
using reactBackend.Models;
using System.ComponentModel.DataAnnotations;

public class Order
{
    public Order()
    {
        Items = new List<OrderItem>();
        OrderDate = DateTime.UtcNow;
        Status = OrderStatus.Pending;
    }

    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;

    public DateTime OrderDate { get; set; }

    [Required]
    public OrderStatus Status { get; set; }

    [Required]
    [Range(0, double.MaxValue)]
    public decimal SubTotal { get; set; }

    [Required]
    [Range(0, double.MaxValue)]
    public decimal VatAmount { get; set; }

    [Required]
    [Range(0, double.MaxValue)]
    public decimal TotalAmount { get; set; }

    [Required]
    [Range(0, double.MaxValue)]
    public decimal DeliveryFee { get; set; }

    // إضافة حقل DiscountAmount للخصومات
    [Range(0, double.MaxValue)]
    public decimal DiscountAmount { get; set; } = 0;

    // إضافة المرجع إلى PromoCode
    public int? PromoCodeId { get; set; }
    public virtual PromoCode? PromoCode { get; set; }

    // تعديل FinalAmount ليأخذ في الاعتبار مبلغ الخصم
// الصيغة الصحيحة لحساب الإجمالي النهائي
public decimal FinalAmount => TotalAmount + DeliveryFee;
    public virtual List<OrderItem> Items { get; set; }
    public virtual PaymentDetails? PaymentDetails { get; set; }
    public virtual DeliveryAddress? Address { get; set; }
    public virtual ApplicationUser? User { get; set; }
    public string? InvoicePath { get; set; }
}