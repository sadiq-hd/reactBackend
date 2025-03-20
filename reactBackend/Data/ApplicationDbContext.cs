using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using reactBackend.Models;
using reactBackend.Models.Enums;
using System.Text.Json;

namespace reactBackend.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // الـ DBSet الحالية
        public DbSet<Product> Products { get; set; } = null!;
        public DbSet<ProductImage> ProductImages { get; set; } = null!;
        public DbSet<WishlistItem> WishlistItems { get; set; } = null!;
        public DbSet<CartItem> CartItems { get; set; } = null!;
        public DbSet<Order> Orders { get; set; } = null!;
        public DbSet<OrderItem> OrderItems { get; set; } = null!;
        public DbSet<PaymentDetails> PaymentDetails { get; set; } = null!;
        public DbSet<DeliveryAddress> DeliveryAddresses { get; set; } = null!;
        public DbSet<UserAddress> UserAddresses { get; set; } = null!;
        public DbSet<Discount> Discounts { get; set; } = null!;
        public DbSet<DiscountProduct> DiscountProducts { get; set; } = null!;
        public DbSet<PromoCode> PromoCodes { get; set; } = null!;
        public DbSet<PromoCodeUsage> PromoCodeUsages { get; set; } = null!;

        // إضافة DbSet الجديدة
        public DbSet<ProductComment> ProductComments { get; set; } = null!;
        public DbSet<ProductReview> ProductReviews { get; set; } = null!;
        public DbSet<CommentLike> CommentLikes { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // الإعدادات الحالية للكيانات
            modelBuilder.Entity<Order>()
                .Property(o => o.InvoicePath)
                .HasMaxLength(500);

            // تكوين العلاقات لعناوين المستخدمين
            modelBuilder.Entity<UserAddress>()
                .HasOne(a => a.User)
                .WithMany() // لم نقم بإضافة الملكية المقابلة في ApplicationUser
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // تكوين العلاقات للمستخدم والطلبات
            modelBuilder.Entity<Order>()
                .HasOne(o => o.User)
                .WithMany(u => u.Orders)
                .HasForeignKey(o => o.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // تكوين العلاقات للطلب والعناصر
            modelBuilder.Entity<Order>()
                .HasMany(o => o.Items)
                .WithOne(i => i.Order)
                .HasForeignKey(i => i.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            // تكوين PaymentDetails
            modelBuilder.Entity<PaymentDetails>(entity =>
            {
                entity.HasOne(p => p.Order)
                    .WithOne(o => o.PaymentDetails)
                    .HasForeignKey<PaymentDetails>(p => p.OrderId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.Property(p => p.Status)
                    .HasConversion(
                        v => v.ToString(),
                        v => (PaymentStatus)Enum.Parse(typeof(PaymentStatus), v))
                    .IsRequired();

                entity.Property(p => p.PaymentMethod)
                    .HasConversion(
                        v => v.ToString(),
                        v => (PaymentMethodType)Enum.Parse(typeof(PaymentMethodType), v))
                    .IsRequired();

                entity.Property(p => p.PaymentData)
                    .HasColumnType("nvarchar(max)")
                    .HasConversion(
                        v => v != null ? JsonSerializer.Serialize(v, new JsonSerializerOptions { WriteIndented = true }) : null,
                        v => v != null ? JsonSerializer.Deserialize<Dictionary<string, string>>(v, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) : new Dictionary<string, string>())
                    .Metadata.SetValueComparer(new DictionaryComparer<string, string>());

                entity.Property(p => p.Amount)
                    .HasColumnType("decimal(18,2)")
                    .IsRequired();

                entity.Property(p => p.RefundAmount)
                    .HasColumnType("decimal(18,2)")
                    .IsRequired(false);

                entity.Property(p => p.TransactionId)
                    .HasMaxLength(100);

                entity.Property(p => p.PaidAt)
                    .HasColumnType("datetime2");
            });

            // تكوين العلاقات للخصومات
            modelBuilder.Entity<DiscountProduct>()
                .HasKey(dp => new { dp.DiscountId, dp.ProductId });

            modelBuilder.Entity<DiscountProduct>()
                .HasOne(dp => dp.Discount)
                .WithMany(d => d.Products)
                .HasForeignKey(dp => dp.DiscountId);

            modelBuilder.Entity<DiscountProduct>()
                .HasOne(dp => dp.Product)
                .WithMany()
                .HasForeignKey(dp => dp.ProductId);

            // تكوين خصائص Discount
            modelBuilder.Entity<Discount>()
                .Property(d => d.Value)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<Discount>()
                .Property(d => d.Type)
                .HasConversion(
                    v => v.ToString(),
                    v => (DiscountType)Enum.Parse(typeof(DiscountType), v));

            modelBuilder.Entity<Discount>()
                .Property(d => d.Scope)
                .HasConversion(
                    v => v.ToString(),
                    v => (DiscountScope)Enum.Parse(typeof(DiscountScope), v));

            // تكوين العلاقات لأكواد الخصم واستخداماتها
            modelBuilder.Entity<PromoCodeUsage>()
                .HasOne(u => u.PromoCode)
                .WithMany(p => p.Usages)
                .HasForeignKey(u => u.PromoCodeId);

            modelBuilder.Entity<PromoCodeUsage>()
                .HasOne(u => u.Order)
                .WithMany()
                .HasForeignKey(u => u.OrderId);

            // تكوين خصائص PromoCode
            modelBuilder.Entity<PromoCode>()
                .Property(p => p.Value)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<PromoCode>()
                .Property(p => p.MinimumOrderAmount)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<PromoCode>()
                .Property(p => p.Type)
                .HasConversion(
                    v => v.ToString(),
                    v => (PromoCodeType)Enum.Parse(typeof(PromoCodeType), v));

            // إنشاء Unique Index على حقل Code في جدول PromoCodes
            modelBuilder.Entity<PromoCode>()
                .HasIndex(p => p.Code)
                .IsUnique();

            // تكوين العلاقات للطلب وعنوان التوصيل
            modelBuilder.Entity<Order>()
                .HasOne(o => o.Address)
                .WithOne(a => a.Order)
                .HasForeignKey<DeliveryAddress>(a => a.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            // تكوين العلاقات للسلة والمفضلة
            ConfigureCartItemEntity(modelBuilder);
            ConfigureWishlistItemEntity(modelBuilder);

            // تكوين العلاقات لصور المنتجات
            modelBuilder.Entity<ProductImage>()
                .HasOne(pi => pi.Product)
                .WithMany(p => p.Images)
                .HasForeignKey(pi => pi.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            // تكوين خصائص الطلب
            ConfigureOrderEntity(modelBuilder);
            ConfigureOrderItemEntity(modelBuilder);
            ConfigureProductEntity(modelBuilder);

            // تكوين العلاقات الجديدة للتعليقات والتقييمات
            ConfigureProductCommentEntity(modelBuilder);
            ConfigureProductReviewEntity(modelBuilder);
            ConfigureCommentLikeEntity(modelBuilder);
        }

        private void ConfigureCartItemEntity(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CartItem>(entity =>
            {
                entity.HasOne<ApplicationUser>(c => c.User)
                    .WithMany(u => u.CartItems)
                    .HasForeignKey(c => c.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(c => c.Product)
                    .WithMany(p => p.CartItems)
                    .HasForeignKey(c => c.ProductId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(c => new { c.UserId, c.ProductId }).IsUnique();
            });
        }

        private void ConfigureWishlistItemEntity(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<WishlistItem>(entity =>
            {
                entity.HasOne<ApplicationUser>(w => w.User)
                    .WithMany(u => u.WishlistItems)
                    .HasForeignKey(w => w.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(w => w.Product)
                    .WithMany(p => p.WishlistItems)
                    .HasForeignKey(w => w.ProductId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(w => new { w.UserId, w.ProductId }).IsUnique();
            });
        }

        private void ConfigureOrderEntity(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Order>()
                .Property(o => o.TotalAmount)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<Order>()
                .Property(o => o.DeliveryFee)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<Order>()
                .Property(o => o.Status)
                .HasConversion(
                    v => v.ToString(),
                    v => (OrderStatus)Enum.Parse(typeof(OrderStatus), v));

            modelBuilder.Entity<Order>()
                .HasIndex(o => o.UserId);
        }

        private void ConfigureOrderItemEntity(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<OrderItem>()
                .Property(oi => oi.Price)
                .HasColumnType("decimal(18,2)");
        }

        private void ConfigureProductEntity(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Product>()
                .Property(p => p.Price)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<Product>()
                .HasIndex(p => p.Category);

            // إضافة تكوين لمتوسط التقييم
            modelBuilder.Entity<Product>()
                .Property(p => p.AverageRating)
                .HasColumnType("decimal(3,2)");
        }

        // تكوين كيان التعليقات
        private void ConfigureProductCommentEntity(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ProductComment>(entity =>
            {
                entity.HasOne(c => c.Product)
                    .WithMany(p => p.Comments)
                    .HasForeignKey(c => c.ProductId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(c => c.User)
                    .WithMany(u => u.Comments)
                    .HasForeignKey(c => c.UserId)
                    .OnDelete(DeleteBehavior.Restrict);

                // تكوين العلاقة مع التعليقات الفرعية (الردود)
                entity.HasOne(c => c.ParentComment)
                    .WithMany(c => c.Replies)
                    .HasForeignKey(c => c.ParentCommentId)
                     .OnDelete(DeleteBehavior.ClientSetNull)
                    .IsRequired(false);

                // تحويل قيمة CommentStatus إلى نص
                entity.Property(c => c.Status)
                    .HasConversion(
                        v => v.ToString(),
                        v => (CommentStatus)Enum.Parse(typeof(CommentStatus), v));

                // إنشاء فهرس للبحث السريع
                entity.HasIndex(c => c.ProductId);
                entity.HasIndex(c => c.UserId);
                entity.HasIndex(c => c.Status);
            });
        }

        // تكوين كيان التقييمات
        private void ConfigureProductReviewEntity(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ProductReview>(entity =>
            {
                entity.HasOne(r => r.Product)
                    .WithMany(p => p.Reviews)
                    .HasForeignKey(r => r.ProductId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(r => r.User)
                    .WithMany(u => u.Reviews)
                    .HasForeignKey(r => r.UserId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(r => r.Order)
                    .WithMany()
                    .HasForeignKey(r => r.OrderId)
                    .OnDelete(DeleteBehavior.Restrict);

                // تحويل قيمة ReviewStatus إلى نص
                entity.Property(r => r.Status)
                    .HasConversion(
                        v => v.ToString(),
                        v => (ReviewStatus)Enum.Parse(typeof(ReviewStatus), v));

                // ضمان أن المستخدم لديه تقييم واحد فقط لكل منتج
                entity.HasIndex(r => new { r.UserId, r.ProductId }).IsUnique();

                // إنشاء فهرس للبحث السريع
                entity.HasIndex(r => r.ProductId);
                entity.HasIndex(r => r.Rating);
            });
        }

        // تكوين كيان الإعجابات بالتعليقات
        private void ConfigureCommentLikeEntity(ModelBuilder modelBuilder)
        {
modelBuilder.Entity<CommentLike>(entity =>
    {
        entity.HasOne(l => l.Comment)
            .WithMany(c => c.Likes) // تأكد من أنك أضفت هذه العلاقة في ProductComment
            .HasForeignKey(l => l.CommentId)
            .OnDelete(DeleteBehavior.ClientSetNull); // تغيير من Cascade

                entity.HasOne(l => l.User)
                    .WithMany(u => u.CommentLikes)
                    .HasForeignKey(l => l.UserId)
                    .OnDelete(DeleteBehavior.Restrict);

                // ضمان أن المستخدم لديه إعجاب واحد فقط لكل تعليق
                entity.HasIndex(l => new { l.UserId, l.CommentId }).IsUnique();
            });
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder.ConfigureWarnings(warnings =>
                    warnings.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning));
            }
        }
    }

    public class DictionaryComparer<TKey, TValue> : ValueComparer<Dictionary<TKey, TValue>> where TKey : notnull
    {
        public DictionaryComparer() : base(
            (d1, d2) => d1 != null && d2 != null && d1.SequenceEqual(d2),
            d => d != null ? d.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())) : 0,
            d => d != null ? new Dictionary<TKey, TValue>(d) : new Dictionary<TKey, TValue>())
        {
        }
    }
}