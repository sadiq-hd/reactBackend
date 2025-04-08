using reactBackend.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace reactBackend.Dtos
{
    public class CreatePaymentDto
    {
        [Required(ErrorMessage = "طريقة الدفع مطلوبة")]
        public PaymentMethodType PaymentMethod { get; set; }
        public Dictionary<string, string>? PaymentDetails { get; set; }
    }

    public class PaymentResponseDto
    {
        public int Id { get; set; }
        public PaymentStatus Status { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "SAR";
        public DateTime CreatedAt { get; set; }
        public string? TransactionId { get; set; }
        public PaymentProvider? Provider { get; set; }
        public string? ErrorMessage { get; set; }
        public bool IsSuccessful => Status == PaymentStatus.Completed;
        public bool IsPending => Status == PaymentStatus.Pending;
        public bool IsFailed => Status == PaymentStatus.Failed;
    }

    public class PaymentSearchDto
    {
        public PaymentStatus? Status { get; set; }
        public PaymentMethodType? PaymentMethod { get; set; }
        [DataType(DataType.Date)]
        public DateTime? FromDate { get; set; }
        [DataType(DataType.Date)]
        public DateTime? ToDate { get; set; }
        [Range(0, double.MaxValue)]
        public decimal? MinAmount { get; set; }
        [Range(0, double.MaxValue)]
        public decimal? MaxAmount { get; set; }
    }

    public class UpdatePaymentStatusDto
    {
        public  PaymentStatus NewStatus { get; set; }
        public string? TransactionId { get; set; }
        public string? ErrorMessage { get; set; }
        public Dictionary<string, string>? AdditionalData { get; set; }
    }
}