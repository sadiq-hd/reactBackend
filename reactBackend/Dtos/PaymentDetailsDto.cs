using System;

namespace reactBackend.Dtos
{
    public class PaymentDetailsDto
    {
        public string PaymentMethod { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime? PaidAt { get; set; }
        public string? TransactionId { get; set; }
        public string? ErrorMessage { get; set; }
        public bool IsRefunded { get; set; }
        public DateTime? RefundedAt { get; set; }
        public decimal? RefundAmount { get; set; }
    }
}