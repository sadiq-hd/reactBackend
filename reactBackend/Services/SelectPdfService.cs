using Microsoft.Extensions.Logging;
using SelectPdf;
using System;
using System.IO;
using System.Threading.Tasks;

namespace reactBackend.Services
{
    public class SelectPdfService : IPdfService
    {
        private readonly ILogger<SelectPdfService> _logger;

        public SelectPdfService(ILogger<SelectPdfService> logger)
        {
            _logger = logger;
        }

        public byte[] GeneratePdf(string htmlContent, string title)
        {
            try
            {
                _logger.LogInformation($"بدء إنشاء ملف PDF: {title}");

                // إضافة عنوان المستند كترويسة في HTML
                string htmlWithHeader = $@"
                <!DOCTYPE html>
                <html>
                <head>
                    <meta charset='utf-8'>
                    <title>{title}</title>
                    <style>
                        body {{ font-family: Arial, sans-serif; }}
                        .header {{ text-align: center; padding: 10px; border-bottom: 1px solid #ddd; margin-bottom: 20px; }}
                        .footer {{ text-align: center; font-size: 12px; color: #999; margin-top: 20px; padding-top: 10px; border-top: 1px solid #ddd; }}
                    </style>
                </head>
                <body>
                    <div class='header'>
                        <h2>{title}</h2>
                    </div>
                    {htmlContent}
                    <div class='footer'>
                        © {DateTime.Now.Year} المتجر
                    </div>
                </body>
                </html>";

                // إنشاء المحول
                HtmlToPdf converter = new HtmlToPdf();
                
                // تكوين الخيارات
                converter.Options.PdfPageSize = PdfPageSize.A4;
                converter.Options.PdfPageOrientation = PdfPageOrientation.Portrait;
                converter.Options.MarginTop = 10;
                converter.Options.MarginBottom = 10;
                converter.Options.MarginLeft = 10;
                converter.Options.MarginRight = 10;
                
                // تنفيذ التحويل
                PdfDocument document = converter.ConvertHtmlString(htmlWithHeader);
                
                // تحويل المستند إلى مصفوفة بايت
                byte[] pdfBytes = document.Save();
                document.Close();
                
                _logger.LogInformation($"تم إنشاء ملف PDF بنجاح: {title} (حجم: {pdfBytes.Length} بايت)");
                return pdfBytes;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"خطأ في إنشاء ملف PDF: {title}");
                throw;
            }
        }

        public async Task SavePdfAsync(string htmlContent, string filePath, string title)
        {
            try
            {
                _logger.LogInformation($"بدء حفظ ملف PDF: {title} في المسار: {filePath}");
                
                // إنشاء المجلد إذا لم يكن موجوداً
                string? directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                
                byte[] pdfBytes = GeneratePdf(htmlContent, title);
                await File.WriteAllBytesAsync(filePath, pdfBytes);
                
                _logger.LogInformation($"تم حفظ ملف PDF بنجاح: {filePath}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"خطأ في حفظ ملف PDF: {filePath}");
                throw;
            }
        }
    }
}