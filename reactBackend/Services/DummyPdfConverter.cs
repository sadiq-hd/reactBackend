using Microsoft.Extensions.Logging;
using System.Text;

namespace reactBackend.Services
{
    public class DummyPdfConverter
    {
        private readonly ILogger<DummyPdfConverter> _logger;

        public DummyPdfConverter(ILogger<DummyPdfConverter> logger = null)
        {
            _logger = logger ?? LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<DummyPdfConverter>();
        }

        public byte[] GenerateDummyPdf(string contentInfo)
        {
            _logger.LogWarning("استخدام محول PDF وهمي. لن يتم إنشاء PDF حقيقي.");
            return Encoding.UTF8.GetBytes($"This is a dummy PDF file content for: {contentInfo}");
        }
    }
}