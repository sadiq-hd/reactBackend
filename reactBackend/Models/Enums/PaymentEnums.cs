namespace reactBackend.Models.Enums
{
    public enum OrderStatus
    {
        Pending,       // قيد الانتظار
        Processing,    // قيد المعالجة
        Shipped,       // تم الشحن
        Delivered,     // تم التوصيل
        Cancelled     // ملغي
    }

    public enum PaymentMethodType
    {
        CREDIT_CARD = 0,
        MADA = 1,
        APPLE_PAY = 2,
        SAMSUNG_PAY = 3,
        GOOGLE_PAY = 4,
        STC_PAY = 5,
        CASH_ON_DELIVERY = 6,
        PAYPAL = 7
    }

    public enum PaymentStatus
    {
        Pending,        // معلق
        Processing,     // قيد المعالجة
        Completed,      // مكتمل
        Failed,        // فشل
        Refunded       // مسترجع
    }

    public enum PaymentProvider
    {
        Visa,
        Mastercard,
        Mada,
        ApplePay,
        SamsungPay,
        GooglePay,
        StcPay,
        PayPal,
        Cash
    }
}