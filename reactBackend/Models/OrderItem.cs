namespace reactBackend.Models
{
    public class OrderItem
    {
        public int Id { get; set; }
        public int OrderId { get; set; }
        public int ProductId { get; set; }
        public string? ProductName { get; set; } // إضافة اسم المنتج
        public int Quantity { get; set; }
        public decimal Price { get; set; } // السعر بعد الخصم
        public decimal OriginalPrice { get; set; } // السعر الأصلي قبل الخصم
        public decimal Total { get; set; } // المجموع بعد الخصم
        public decimal DiscountAmount { get; set; } // مبلغ الخصم
        public string? DiscountName { get; set; } // إضافة اسم الخصم
        public int? DiscountId { get; set; } // معرف الخصم المطبق (إذا وجد)
        public virtual Product? Product { get; set; }
        public virtual Order? Order { get; set; }
        public virtual Discount? Discount { get; set; }
    }
}