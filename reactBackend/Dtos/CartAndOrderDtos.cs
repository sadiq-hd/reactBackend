using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using reactBackend.Models.Enums;
using System.Text.Json;

namespace reactBackend.Dtos
{
    public class AddToCartDto
    {
        [Required]
        public int ProductId { get; set; }

        [Required]
        [Range(1, int.MaxValue)]
        public int Quantity { get; set; }
    }

    public class CartItemDto
    {
        public int ProductId { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int Quantity { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
        public int Stock { get; set; }
        public bool HasDiscount { get; set; }
        public decimal? DiscountedPrice { get; set; }
        public decimal? DiscountValue { get; set; }
        public string? DiscountType { get; set; }
        public string? DiscountName { get; set; }
    }

    public class CartItemWithDiscountDto
    {
        public int ProductId { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; } // السعر الأصلي
        public decimal DiscountedPrice { get; set; } // السعر بعد الخصم
        public int Quantity { get; set; }
        public decimal Total { get; set; } // المجموع الأصلي
        public decimal DiscountedTotal { get; set; } // المجموع بعد الخصم
        public string ImageUrl { get; set; } = string.Empty;
        public int Stock { get; set; }
        public bool HasDiscount { get; set; }
        public string DiscountName { get; set; } = string.Empty;
        public decimal? DiscountValue { get; set; }
        public string DiscountType { get; set; } = string.Empty;
        public decimal DiscountAmount { get; set; } // مبلغ الخصم
    }

    public class CreateOrderItemDto
    {
        [Required]
        public int ProductId { get; set; }

        [Required]
        [Range(1, int.MaxValue)]
        public int Quantity { get; set; }
    }

    public class CreateOrderDto
    {
        [Required]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public PaymentMethodType PaymentMethod { get; set; }

        [Required]
        [ValidatePaymentDetails]
        public Dictionary<string, string>? PaymentDetails { get; set; }

        [Required]
        public DeliveryAddressDto Address { get; set; } = null!;

        [Required]
        public List<CreateOrderItemDto> Items { get; set; } = new();
    }

    public class ValidatePaymentDetailsAttribute : ValidationAttribute
    {
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            var orderDto = (CreateOrderDto)validationContext.ObjectInstance;
            var paymentDetails = value as Dictionary<string, string>;

            if (paymentDetails == null && orderDto.PaymentMethod != PaymentMethodType.APPLE_PAY
                && orderDto.PaymentMethod != PaymentMethodType.GOOGLE_PAY
                && orderDto.PaymentMethod != PaymentMethodType.SAMSUNG_PAY
                && orderDto.PaymentMethod != PaymentMethodType.PAYPAL)
            {
                return new ValidationResult("بيانات الدفع مطلوبة");
            }

            switch (orderDto.PaymentMethod)
            {
                case PaymentMethodType.CREDIT_CARD:
                case PaymentMethodType.MADA:
                    return ValidateCardPayment(paymentDetails);

                case PaymentMethodType.STC_PAY:
                case PaymentMethodType.CASH_ON_DELIVERY:
                    return ValidatePhoneNumber(paymentDetails);

                default:
                    return ValidationResult.Success;
            }
        }

        private ValidationResult? ValidateCardPayment(Dictionary<string, string>? details)
        {
            if (details == null || !details.ContainsKey("cardNumber")
                || !details.ContainsKey("expiryDate")
                || !details.ContainsKey("cvv"))
            {
                return new ValidationResult("جميع بيانات البطاقة مطلوبة");
            }

            var cardNumber = details["cardNumber"].Replace(" ", "");
            if (!System.Text.RegularExpressions.Regex.IsMatch(cardNumber, @"^\d{16}$"))
            {
                return new ValidationResult("رقم البطاقة غير صحيح");
            }

            var expiryDate = details["expiryDate"];
            if (!System.Text.RegularExpressions.Regex.IsMatch(expiryDate, @"^(0[1-9]|1[0-2])\/([0-9]{2})$"))
            {
                return new ValidationResult("تاريخ انتهاء البطاقة غير صحيح");
            }

            var cvv = details["cvv"];
            if (!System.Text.RegularExpressions.Regex.IsMatch(cvv, @"^[0-9]{3}$"))
            {
                return new ValidationResult("رمز الأمان CVV غير صحيح");
            }

            return ValidationResult.Success;
        }

        private ValidationResult? ValidatePhoneNumber(Dictionary<string, string>? details)
        {
            if (details == null || !details.ContainsKey("phone"))
            {
                return new ValidationResult("رقم الجوال مطلوب");
            }

            var phone = details["phone"];
            if (!System.Text.RegularExpressions.Regex.IsMatch(phone, @"^05\d{8}$"))
            {
                return new ValidationResult("رقم الجوال غير صحيح");
            }

            return ValidationResult.Success;
        }
    }

    public class DeliveryAddressDto
    {
        [Required]
        [StringLength(100)]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [RegularExpression(@"^05\d{8}$", ErrorMessage = "رقم الجوال غير صحيح")]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string City { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string Street { get; set; } = string.Empty;

        [StringLength(20)]
        public string? BuildingNumber { get; set; }

        [StringLength(200)]
        public string? AdditionalDetails { get; set; }
    }

    public class OrderResponseDto
    {
        public int Id { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime OrderDate { get; set; }
        public decimal SubTotal { get; set; }
        public decimal DiscountAmount { get; set; } // مبلغ الخصم الإجمالي
        public decimal VatAmount { get; set; }
        public decimal TotalAmount { get; set; } // المبلغ بعد الخصم وقبل الضريبة
        public decimal DeliveryFee { get; set; }
        public decimal FinalAmount { get; set; } // المبلغ النهائي شامل الضريبة والتوصيل
        public string PaymentStatus { get; set; } = string.Empty;
        public string PaymentMethod { get; set; } = string.Empty;
        public List<OrderItemDto> Items { get; set; } = new();
        public DeliveryAddressDto? DeliveryAddress { get; set; }
        public PaymentDetailsDto? PaymentDetails { get; set; }
        public bool HasDiscount { get; set; } // هل يحتوي الطلب على خصم
    }

    public class OrderItemDto
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public decimal Price { get; set; } // السعر بعد الخصم
        public decimal? OriginalPrice { get; set; } // السعر الأصلي قبل الخصم
        public int Quantity { get; set; }
        public decimal Total { get; set; } // المجموع بعد الخصم
        public decimal? DiscountAmount { get; set; } // مبلغ الخصم
        public decimal VatAmount { get; set; } // تغيير من خاصية محسوبة إلى خاصية عادية
        public decimal TotalWithVat { get; set; } // تغيير من خاصية محسوبة إلى خاصية عادية
        public bool HasDiscount => DiscountAmount.HasValue && DiscountAmount.Value > 0;
    }



}