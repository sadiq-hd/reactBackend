using Microsoft.Extensions.Logging;
using System;
using System.Text;

namespace reactBackend.Services
{
    /// <summary>
    /// فئة بديلة لـ PdfConverter عندما لا تكون المكتبة الأصلية متاحة
    /// </summary>
    public class DummyPdfConverter
    {
        private readonly ILogger<DummyPdfConverter> _logger;

        public DummyPdfConverter(ILogger<DummyPdfConverter> logger = null)
        {
            _logger = logger;
        }

        public byte[] GenerateDummyPdf(string title)
        {
            try
            {
                // إنشاء PDF وهمي بسيط
                string dummyContent = "%PDF-1.4\n1 0 obj<</Type/Catalog/Pages 2 0 R>>endobj 2 0 obj<</Type/Pages/Kids[3 0 R]/Count 1>>endobj 3 0 obj<</Type/Page/MediaBox[0 0 595 842]/Resources<<>>/Contents 4 0 R/Parent 2 0 R>>endobj 4 0 obj<</Length 100>>stream\nBT /F1 12 Tf 50 800 Td (Dummy PDF - " + title + " - Generated at " + DateTime.Now.ToString() + ") Tj ET\nendstream\nendobj\nxref\n0 5\n0000000000 65535 f\n0000000010 00000 n\n0000000053 00000 n\n0000000102 00000 n\n0000000192 00000 n\ntrailer<</Size 5/Root 1 0 R>>\nstartxref\n292\n%%EOF";
                
                _logger.LogInformation($"تم إنشاء PDF وهمي: {title}");
                return Encoding.ASCII.GetBytes(dummyContent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في إنشاء PDF وهمي");
                // إرجاع PDF وهمي بسيط جداً
                return Encoding.ASCII.GetBytes("%PDF-1.4\n1 0 obj<</Type/Catalog>>endobj\nxref\n0 2\n0000000000 65535 f\n0000000010 00000 n\ntrailer<</Size 2/Root 1 0 R>>\nstartxref\n50\n%%EOF");
            }
        }
    }
}