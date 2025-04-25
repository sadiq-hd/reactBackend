using System;
using System.Threading.Tasks;

namespace reactBackend.Services
{
    public interface IPdfService
    {
        /// <summary>
        /// إنشاء محتوى PDF من HTML وإرجاعه كمصفوفة بايت
        /// </summary>
        byte[] GeneratePdf(string htmlContent, string title);

        /// <summary>
        /// إنشاء محتوى PDF من HTML وحفظه في ملف
        /// </summary>
        Task SavePdfAsync(string htmlContent, string filePath, string title);
    }
}