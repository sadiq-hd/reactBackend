using Microsoft.EntityFrameworkCore;
using reactBackend.Data;
using reactBackend.Models.Enums;

namespace reactBackend.Services
{
    public interface IPurchaseVerificationService
    {
        Task<bool> HasUserPurchasedProduct(string userId, int productId);
        Task<IEnumerable<int>> GetUserPurchasedProductIds(string userId);
        Task<IDictionary<int, DateTime>> GetProductPurchaseDetails(string userId, IEnumerable<int> productIds);
    }

    public class PurchaseVerificationService : IPurchaseVerificationService
    {
        private readonly ApplicationDbContext _context;

        public PurchaseVerificationService(ApplicationDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// التحقق مما إذا كان المستخدم قد اشترى منتج معين
        /// </summary>
        public async Task<bool> HasUserPurchasedProduct(string userId, int productId)
        {
            return await _context.Orders
                .Where(o => o.UserId == userId && o.Status == OrderStatus.Delivered)
                .SelectMany(o => o.Items)
                .AnyAsync(i => i.ProductId == productId);
        }

        /// <summary>
        /// الحصول على قائمة بمعرّفات المنتجات التي اشتراها المستخدم
        /// </summary>
        public async Task<IEnumerable<int>> GetUserPurchasedProductIds(string userId)
        {
            return await _context.Orders
                .Where(o => o.UserId == userId && o.Status == OrderStatus.Delivered)
                .SelectMany(o => o.Items)
                .Select(i => i.ProductId)
                .Distinct()
                .ToListAsync();
        }

        /// <summary>
        /// الحصول على تفاصيل شراء منتجات معينة للمستخدم
        /// تعيد قاموس يحتوي على معرّف المنتج وتاريخ آخر شراء
        /// </summary>
        public async Task<IDictionary<int, DateTime>> GetProductPurchaseDetails(string userId, IEnumerable<int> productIds)
        {
            var purchaseDetails = new Dictionary<int, DateTime>();

            var orders = await _context.Orders
                .Include(o => o.Items)
                .Where(o => o.UserId == userId && o.Status == OrderStatus.Delivered)
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            foreach (var productId in productIds)
            {
                // البحث عن آخر طلب يحتوي على هذا المنتج
                var latestOrder = orders
                    .Where(o => o.Items.Any(i => i.ProductId == productId))
                    .OrderByDescending(o => o.OrderDate)
                    .FirstOrDefault();

                if (latestOrder != null)
                {
                    purchaseDetails[productId] = latestOrder.OrderDate;
                }
            }

            return purchaseDetails;
        }
    }
}