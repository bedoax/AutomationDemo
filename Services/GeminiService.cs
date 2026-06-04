using AutomationDemo.Interfaces;
using AutomationDemo.Interfaces.AutomationDemo.Interfaces;
using System.Text;
using System.Text.Json;

namespace AutomationDemo.Services
{
    /// <summary>
    /// خدمة التلخيص باستخدام Gemini API مع fallback تلقائي لـ 5 موديلات
    /// </summary>
    public class GeminiService : IGeminiService
    {
        private readonly HttpClient _httpClient;
        private readonly IMessageFilter _filter;
        private readonly ILogger<GeminiService> _logger;
        private readonly string _apiKey;

        // ترتيب الموديلات: الأولوية من الأول للأخير
        private static readonly (string Name, string Model)[] ModelChain =
        {
             ("Primary",   "gemini-3.5-flash"),
             ("Secondary", "gemini-3.1-flash-lite"),
             ("Tertiary",  "gemini-2.5-flash"),       
             ("Quaternary","gemini-2.5-flash-lite"),
             ("Fallback",  "gemini-2.0-flash"),
        };

        private const string BaseUrl =
            "https://generativelanguage.googleapis.com/v1beta/models/{0}:generateContent?key={1}";
        //https://generativelanguage.googleapis.com/v1beta/models/gemini-3.5-flash:generateContent?key=

        private const string SystemPrompt =
            "أنت مساعد ذكي مخصص لتلخيص محادثات جروبات العمل والمجتمعات على واتساب.\n" +
            "اجعل الأسلوب احترافي ومباشر مستخدماً التنسيق بنقاط (•). إليك الرسائل:\n\n";

        public GeminiService(
            IHttpClientFactory httpClientFactory,
            IMessageFilter filter,
            IConfiguration configuration,
            ILogger<GeminiService> logger)
        {
            _httpClient = httpClientFactory.CreateClient(nameof(GeminiService));
            _filter = filter;
            _logger = logger;
            _apiKey = configuration["WhatsAppAutomation:GeminiApiKey"]
                          ?? throw new InvalidOperationException("GeminiApiKey is missing from configuration.");
        }

        public async Task<string> SummarizeAsync(string text)
        {
            _logger.LogInformation("API Key loaded (first 8 chars): {Key}",
             _apiKey.Length >= 8 ? _apiKey[..8] + "..." : "TOO_SHORT");
            string filtered = _filter.Filter(text);

            // تسلسل الـ JSON مرة واحدة فقط — لا تكرار
            string json = BuildRequestJson(filtered);

            foreach (var (name, model) in ModelChain)
            {
                string url = string.Format(BaseUrl, model, _apiKey);
                try
                {
                    _logger.LogInformation("Trying Gemini model [{Name}] ({Model})...", name, model);

                    // StringContent جديد لكل محاولة لأنه IDisposable ولا يُعاد استخدامه
                    using var content = new StringContent(json, Encoding.UTF8, "application/json");
                    using var response = await _httpClient.PostAsync(url, content);

                    if (response.IsSuccessStatusCode)
                    {
                        _logger.LogInformation("Model [{Name}] succeeded.", name);
                        return await ExtractTextAsync(response);
                    }

                    _logger.LogWarning("Model [{Name}] returned {Status}. Trying next...",
                        name, response.StatusCode);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Model [{Name}] threw exception: {Msg}. Trying next...",
                        name, ex.Message);
                }
            }

            _logger.LogError("All Gemini models exhausted. Returning fallback message.");
            return "Gemini API Error: All models are currently unavailable.";
        }

        // =====================================================================
        // Helpers — private
        // =====================================================================

        private static string BuildRequestJson(string filteredText)
        {
            var body = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[]
                        {
                            new { text = SystemPrompt + filteredText }
                        }
                    }
                }
            };
            return JsonSerializer.Serialize(body);
        }

        private static async Task<string> ExtractTextAsync(HttpResponseMessage response)
        {
            string jsonString = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(jsonString);

            return doc.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString()
                ?? throw new InvalidDataException("Gemini returned an empty text field.");
        }
    }
}
