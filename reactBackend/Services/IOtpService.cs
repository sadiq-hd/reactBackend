using System;
using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace reactBackend.Services
{
    public interface IOtpService
    {
        string GenerateOtp();
        void StoreOtp(string phoneNumber, string otp, string purpose = "Authentication");
        bool VerifyOtp(string phoneNumber, string otp, string purpose = "Authentication");
        void RemoveOtp(string phoneNumber, string purpose = "Authentication");
    }

    public class OtpService : IOtpService
    {
        // استخدام ConcurrentDictionary لتخزين رموز OTP بطريقة آمنة للتعامل مع طلبات متزامنة
        private readonly ConcurrentDictionary<string, OtpInfo> _otpStore = new ConcurrentDictionary<string, OtpInfo>();
        
        // مدة صلاحية رمز OTP بالدقائق
        private readonly int _otpExpiryMinutes;

        public OtpService(IConfiguration configuration)
        {
            // قراءة مدة صلاحية الرمز من التكوين، أو استخدام قيمة افتراضية (5 دقائق)
            _otpExpiryMinutes = configuration.GetValue<int>("OtpSettings:ExpiryMinutes", 5);
        }

        // توليد رمز OTP عشوائي مكون من 6 أرقام
public string GenerateOtp()
{
    using var rng = RandomNumberGenerator.Create();
    var bytes = new byte[4];
    rng.GetBytes(bytes);
    
    uint randomNumber = BitConverter.ToUInt32(bytes, 0) % 1000000;
    string otp = randomNumber.ToString("D6");
    
    // تأكد من أن الطول هو 6 أرقام دائمًا
    if (otp.Length != 6)
    {
        throw new InvalidOperationException($"فشل في إنشاء رمز OTP من 6 أرقام. الرمز الحالي: {otp}");
    }
    
    return otp;
}

        // تخزين رمز OTP مع وقت إنشائه والغرض منه
        public void StoreOtp(string phoneNumber, string otp, string purpose = "Authentication")
        {
            if (string.IsNullOrEmpty(phoneNumber) || string.IsNullOrEmpty(otp))
                throw new ArgumentException("رقم الهاتف ورمز OTP مطلوبان");

            var key = GetKey(phoneNumber, purpose);
            var otpInfo = new OtpInfo
            {
                Otp = otp,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddMinutes(_otpExpiryMinutes)
            };

            _otpStore.AddOrUpdate(key, otpInfo, (k, v) => otpInfo);
        }

        // التحقق من صحة رمز OTP ومن أنه لم تنتهِ صلاحيته
        public bool VerifyOtp(string phoneNumber, string otp, string purpose = "Authentication")
        {
            if (string.IsNullOrEmpty(phoneNumber) || string.IsNullOrEmpty(otp))
                return false;

            var key = GetKey(phoneNumber, purpose);
            
            if (!_otpStore.TryGetValue(key, out var otpInfo))
                return false;

            // التحقق من أن الرمز لم تنتهِ صلاحيته
            if (DateTime.UtcNow > otpInfo.ExpiresAt)
                return false;

            // التحقق من تطابق الرمز
            return otpInfo.Otp == otp;
        }

        // إزالة رمز OTP بعد استخدامه بنجاح
        public void RemoveOtp(string phoneNumber, string purpose = "Authentication")
        {
            var key = GetKey(phoneNumber, purpose);
            _otpStore.TryRemove(key, out _);
        }

        // توليد مفتاح فريد لتخزين رمز OTP استناداً إلى رقم الهاتف والغرض
        private string GetKey(string phoneNumber, string purpose)
        {
            return $"{phoneNumber}:{purpose}";
        }

        // فئة لتخزين معلومات رمز OTP
        private class OtpInfo
        {
            public string? Otp { get; set; }
            public DateTime CreatedAt { get; set; }
            public DateTime ExpiresAt { get; set; }
        }
    }
}