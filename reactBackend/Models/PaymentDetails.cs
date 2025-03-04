using reactBackend.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace reactBackend.Models
{
    public class PaymentDetails
    {
        public PaymentDetails()
        {
            PaymentData = new Dictionary<string, string>();
            CreatedAt = DateTime.UtcNow;
            Currency = "SAR";
            Status = PaymentStatus.Pending;
        }

        [Key]
        public int Id { get; set; }

        [Required]
        public int OrderId { get; set; }

        [Required]
        public PaymentMethodType PaymentMethod { get; set; }

        [Required]
        public PaymentStatus Status { get; set; }

        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "المبلغ يجب أن يكون أكبر من صفر")]
        public decimal Amount { get; set; }

        [Required]
        [StringLength(3, MinimumLength = 3)]
        public string Currency { get; set; }

        [Required]
        public DateTime CreatedAt { get; set; }

        public DateTime? PaidAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        [StringLength(100)]
        public string? TransactionId { get; set; }

        [StringLength(100)]
        public string? PaymentProviderReference { get; set; }

        [StringLength(50)]
        public string? ErrorCode { get; set; }

        [StringLength(500)]
        public string? ErrorMessage { get; set; }

        [Required]
        public Dictionary<string, string> PaymentData { get; set; }

        public bool IsRefunded { get; set; }
        public DateTime? RefundedAt { get; set; }

        [StringLength(100)]
        public string? RefundTransactionId { get; set; }

        [Range(0.01, double.MaxValue, ErrorMessage = "مبلغ الاسترجاع يجب أن يكون أكبر من صفر")]
        public decimal? RefundAmount { get; set; }

        public virtual Order? Order { get; set; }

        public bool IsPaid => Status == PaymentStatus.Completed;
        public bool IsPending => Status == PaymentStatus.Pending;
        public bool IsFailed => Status == PaymentStatus.Failed;

        public bool ValidateCardData()
        {
            if (PaymentMethod != PaymentMethodType.CREDIT_CARD && PaymentMethod != PaymentMethodType.MADA)
                return true;

            return PaymentData.ContainsKey("cardNumber") &&
                   PaymentData.ContainsKey("expiryDate") &&
                   PaymentData.ContainsKey("cvv") &&
                   ValidateCardNumber(PaymentData["cardNumber"]) &&
                   ValidateExpiryDate(PaymentData["expiryDate"]) &&
                   ValidateCVV(PaymentData["cvv"]);
        }

        private bool ValidateCardNumber(string cardNumber)
        {
            cardNumber = cardNumber.Replace(" ", "");
            return cardNumber.Length == 16 && cardNumber.All(char.IsDigit);
        }

        private bool ValidateExpiryDate(string expiryDate)
        {
            var isValidFormat = System.Text.RegularExpressions.Regex.IsMatch(expiryDate, @"^(0[1-9]|1[0-2])\/([0-9]{2})$");
            if (!isValidFormat) return false;

            var parts = expiryDate.Split('/');
            if (!int.TryParse(parts[0], out int month) || !int.TryParse(parts[1], out int year)) return false;

            year += 2000;
            var expiryDateTime = new DateTime(year, month, 1).AddMonths(1).AddDays(-1);
            return expiryDateTime > DateTime.UtcNow;
        }

        private bool ValidateCVV(string cvv)
        {
            return System.Text.RegularExpressions.Regex.IsMatch(cvv, @"^[0-9]{3}$");
        }

        public void MarkAsPaid(string transactionId)
        {
            if (Status == PaymentStatus.Completed)
                throw new InvalidOperationException("الدفعة مكتملة بالفعل");

            Status = PaymentStatus.Completed;
            TransactionId = transactionId;
            PaidAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
        }

        public void MarkAsProcessing()
        {
            if (Status != PaymentStatus.Pending)
                throw new InvalidOperationException("لا يمكن معالجة الدفعة في حالتها الحالية");

            Status = PaymentStatus.Processing;
            UpdatedAt = DateTime.UtcNow;
        }

        public void MarkAsFailed(string errorCode, string errorMessage)
        {
            if (Status == PaymentStatus.Completed)
                throw new InvalidOperationException("لا يمكن تحديد الدفعة كفاشلة بعد اكتمالها");

            Status = PaymentStatus.Failed;
            ErrorCode = errorCode;
            ErrorMessage = errorMessage;
            UpdatedAt = DateTime.UtcNow;
        }

        public void ProcessRefund(decimal amount, string refundTransactionId)
        {
            if (Status != PaymentStatus.Completed)
                throw new InvalidOperationException("لا يمكن استرجاع دفعة غير مكتملة");

            if (IsRefunded)
                throw new InvalidOperationException("تم استرجاع الدفعة بالفعل");

            if (amount <= 0 || amount > Amount)
                throw new ArgumentException("مبلغ الاسترجاع غير صالح");

            IsRefunded = true;
            RefundedAt = DateTime.UtcNow;
            RefundTransactionId = refundTransactionId;
            RefundAmount = amount;
            Status = PaymentStatus.Refunded;
            UpdatedAt = DateTime.UtcNow;
        }

        public bool CanBeRefunded()
        {
            return Status == PaymentStatus.Completed && !IsRefunded && (DateTime.UtcNow - PaidAt)?.TotalDays <= 180;
        }

        public void UpdatePaymentData(Dictionary<string, string> newData)
        {
            if (Status != PaymentStatus.Pending)
                throw new InvalidOperationException("لا يمكن تحديث بيانات الدفع بعد بدء المعالجة");

            foreach (var item in newData)
            {
                PaymentData[item.Key] = item.Value;
            }
            UpdatedAt = DateTime.UtcNow;
        }
    }
}