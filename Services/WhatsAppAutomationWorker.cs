using AutomationDemo.Interfaces;

namespace AutomationDemo.Services
{
    /// <summary>
    /// الـ Worker الرئيسي — مسؤول فقط عن:
    ///   1. جدولة التشغيل اليومي (Scheduler)
    ///   2. تنسيق خطوات الـ Pipeline
    /// كل منطق المتصفح → IWhatsAppClient
    /// كل منطق الـ AI  → IGeminiService
    /// </summary>
    public class WhatsAppAutomationWorker : BackgroundService
    {
        private readonly ILogger<WhatsAppAutomationWorker> _logger;
        private readonly IConfiguration _configuration;
        private readonly IWhatsAppClient _whatsApp;
        private readonly IGeminiService _gemini;

        public WhatsAppAutomationWorker(
            ILogger<WhatsAppAutomationWorker> logger,
            IConfiguration configuration,
            IWhatsAppClient whatsApp,
            IGeminiService gemini)
        {
            _logger = logger;
            _configuration = configuration;
            _whatsApp = whatsApp;
            _gemini = gemini;
        }

        // =====================================================================
        // ExecuteAsync — الحلقة الرئيسية: جدولة + pipeline
        // =====================================================================
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                // قراءة الإعدادات في كل دورة عشان تتطبق التغييرات الجديدة فوراً
                var config = LoadConfig();

                DateTime nextRun = ComputeNextRunTime(config.RunHour, config.RunMinute);
                TimeSpan wait = nextRun - DateTime.Now;

                _logger.LogInformation(
                    "[Scheduler] Next run at {NextRun:dd/MM/yyyy HH:mm:ss} — waiting {Hours:F2} hours.",
                    nextRun, wait.TotalHours);

                try
                {
                    await Task.Delay(wait, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // إيقاف طبيعي — مش خطأ
                    _logger.LogInformation("Worker stopped gracefully during wait.");
                    return;
                }

                await RunPipelineAsync(config, stoppingToken);
            }
        }

        // =====================================================================
        // Pipeline — الخطوات الثلاث بالترتيب
        // =====================================================================
        private async Task RunPipelineAsync(WorkerConfig config, CancellationToken ct)
        {
            _logger.LogInformation("--- Pipeline started ---");
            try
            {
                // 1. تهيئة المتصفح وتسجيل الدخول
                await _whatsApp.InitializeAsync();

                // 2. الانتقال للجروب المصدر وسحب الرسائل
                await _whatsApp.NavigateToChatAsync(config.CommunityName, config.SourceSubGroup);
                var messages = await _whatsApp.ScrapeMessagesAsync(config.DaysToScrape);

                _logger.LogInformation("Scraped {Count} messages.", messages.Count);

                if (!messages.Any())
                {
                    _logger.LogWarning("No messages found. Skipping summarization.");
                    return;
                }

                // 3. تلخيص الرسائل عبر Gemini
                string rawText = string.Join("\n", messages);
                string summary = await _gemini.SummarizeAsync(rawText);

                string finalMessage = BuildSummaryMessage(config.SourceSubGroup, messages.Count, summary);

                // 4. الانتقال للجروب الهدف وإرسال الملخص
                await _whatsApp.NavigateToChatAsync(config.CommunityName, config.TargetSubGroup);
                await _whatsApp.SendMessageAsync(finalMessage);

                _logger.LogInformation("--- Pipeline finished successfully ---");
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Pipeline cancelled during execution.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Pipeline Error]: {Message}", ex.Message);
            }
            finally
            {
                // تحرير موارد المتصفح بعد كل run سواء نجح أو فشل
                await _whatsApp.DisposeAsync();
            }
        }

        // =====================================================================
        // StopAsync — إيقاف نظيف
        // =====================================================================
        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Shutting down WhatsApp Automation Worker...");
            await _whatsApp.DisposeAsync();
            await base.StopAsync(cancellationToken);
        }

        // =====================================================================
        // Helpers — private
        // =====================================================================

        private static DateTime ComputeNextRunTime(int hour, int minute)
        {
            DateTime now = DateTime.Now;
            DateTime todayAt = new(now.Year, now.Month, now.Day, hour, minute, 0);
            return now > todayAt ? todayAt.AddDays(1) : todayAt;
        }

        private static string BuildSummaryMessage(string groupName, int messageCount, string summary) =>
            $"🤖 *ملخص تلقائي ذكي لمجموعة ({groupName})*\n\n" +
            $"{summary}\n\n" +
            $"• تم تحليل {messageCount} رسالة بنجاح.\n" +
            $"• تحديث: {DateTime.Now:dd/MM/yyyy HH:mm}";

        private WorkerConfig LoadConfig() => new(
            CommunityName: _configuration["WhatsAppAutomation:CommunityName"] ?? "CognitionX",
            SourceSubGroup: _configuration["WhatsAppAutomation:SourceSubGroup"] ?? "General",
            TargetSubGroup: _configuration["WhatsAppAutomation:TargetSubGroup"] ?? "Resources",
            RunHour: int.Parse(_configuration["WhatsAppAutomation:RunHour"] ?? "19"),
            RunMinute: int.Parse(_configuration["WhatsAppAutomation:RunMinute"] ?? "49"),
            DaysToScrape: int.Parse(_configuration["WhatsAppAutomation:DaysToScrape"] ?? "1")
        );

        // record بسيط للإعدادات بدل 6 متغيرات منفصلة
        private record WorkerConfig(
            string CommunityName,
            string SourceSubGroup,
            string TargetSubGroup,
            int RunHour,
            int RunMinute,
            int DaysToScrape);
    }
}