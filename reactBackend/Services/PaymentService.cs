// Services/PaymentService.cs
using reactBackend.Models;
using reactBackend.Models.Enums;
using System;
using System.Threading.Tasks;
using static reactBackend.Controllers.OrdersController;

namespace reactBackend.Services
{
    public interface IPaymentService
    {
        Task<PaymentResponse> ProcessCreditCardPaymentAsync(PaymentDetails paymentDetails);
    }

    public class PaymentService : IPaymentService
    {
        private readonly ILogger<PaymentService> _logger;

        public PaymentService(ILogger<PaymentService> logger)
        {
            _logger = logger;
        }

        public async Task<PaymentResponse> ProcessCreditCardPaymentAsync(PaymentDetails paymentDetails)
        {
            try
            {
                // هذا محاكاة فقط - في الواقع هنا ستتصل بخدمة دفع خارجية
                // مثل Stripe أو PayPal أو Hyperpay

                // تنفيذ تحقق من بيانات البطاقة
                if (!ValidateCreditCard(paymentDetails.PaymentData))
                {
                    return new PaymentResponse
                    {
                        IsSuccess = false,
                        ErrorMessage = "بيانات البطاقة غير صحيحة",
                        TransactionId = null
                    };
                }

                // محاكاة الاتصال بخدمة الدفع (في الواقع سيكون API خارجي)
                await Task.Delay(1000); // محاكاة تأخير الشبكة

                // محاكاة استجابة ناجحة
                return new PaymentResponse
                {
                    IsSuccess = true,
                    TransactionId = $"CARD-{Guid.NewGuid()}",
                    ErrorMessage = null
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing credit card payment");
                return new PaymentResponse
                {
                    IsSuccess = false,
                    ErrorMessage = "حدث خطأ أثناء معالجة الدفع بالبطاقة",
                    TransactionId = null
                };
            }
        }

        private bool ValidateCreditCard(Dictionary<string, string> paymentData)
        {
            if (paymentData == null)
                return false;

            // التحقق من رقم البطاقة
            if (!paymentData.TryGetValue("cardNumber", out var cardNumber) ||
                string.IsNullOrWhiteSpace(cardNumber))
                return false;

            cardNumber = cardNumber.Replace(" ", "");
            if (!System.Text.RegularExpressions.Regex.IsMatch(cardNumber, @"^\d{16}$"))
                return false;

            // التحقق من تاريخ الصلاحية
            if (!paymentData.TryGetValue("expiryDate", out var expiryDate) ||
                string.IsNullOrWhiteSpace(expiryDate))
                return false;

            if (!System.Text.RegularExpressions.Regex.IsMatch(expiryDate, @"^(0[1-9]|1[0-2])\/([0-9]{2})$"))
                return false;

            // التحقق من رمز التحقق CVV
            if (!paymentData.TryGetValue("cvv", out var cvv) ||
                string.IsNullOrWhiteSpace(cvv))
                return false;

            if (!System.Text.RegularExpressions.Regex.IsMatch(cvv, @"^[0-9]{3}$"))
                return false;

            return true;
        }
    }
}