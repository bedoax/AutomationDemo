using PuppeteerSharp;
using AutomationDemo.Interfaces;

namespace AutomationDemo.Services
{
    /// <summary>
    /// تنفيذ IWhatsAppClient باستخدام PuppeteerSharp + Microsoft Edge
    /// يتولى كل عمليات المتصفح: التهيئة، التنقل، السحب، الإرسال
    /// </summary>
    public class WhatsAppClient : IWhatsAppClient
    {
        private readonly ILogger<WhatsAppClient> _logger;
        private readonly string _edgePath;
        private readonly string _userDataDir;

        private IBrowser? _browser;
        private IPage? _page;

        // =====================================================================
        // JS helpers — معرَّفة مرة واحدة هنا بدل ما تتكرر في أكتر من مكان
        // =====================================================================

        // JS لسحب عنوان المحادثة الحالية
        private const string JsGetChatTitle = @"() => {
            const el = document.querySelector(
                ""[data-testid='conversation-info-header-chat-title'] span, "" +
                ""header span[dir='auto']""
            );
            return el ? el.innerText.trim() : 'unknown';
        }";



        public WhatsAppClient(
            IConfiguration configuration,
            ILogger<WhatsAppClient> logger)
        {
            _logger = logger;
            _edgePath = configuration["WhatsAppAutomation:EdgePath"]
                           ?? @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe";
            _userDataDir = Path.Combine(Directory.GetCurrentDirectory(), "WhatsAppUserData");
        }

        // =====================================================================
        // Initialize — يتنادى مرة واحدة في أول الـ pipeline
        // =====================================================================
        public async Task InitializeAsync()
        {
            if (!File.Exists(_edgePath))
                throw new FileNotFoundException($"Edge not found at: {_edgePath}");

            _logger.LogInformation("Launching Edge browser...");

            _browser = await Puppeteer.LaunchAsync(new LaunchOptions
            {
                Headless = false,
                ExecutablePath = _edgePath,
                UserDataDir = _userDataDir,
                Args = new[]
                {
                    "--no-sandbox",
                    "--disable-setuid-sandbox",
                    "--start-maximized",
                    "--disable-extensions"
                }
            });

            _page = await _browser.NewPageAsync();
            await _page.SetViewportAsync(new ViewPortOptions { Width = 1920, Height = 1080 });
            await _page.SetUserAgentAsync(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
                "(KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");

            _logger.LogInformation("Navigating to WhatsApp Web...");
            await _page.GoToAsync("https://web.whatsapp.com/");

            // انتظار اختفاء شاشة التحميل
            await _page.WaitForSelectorAsync(
                "#wa_web_initial_startup",
                new WaitForSelectorOptions { Hidden = true, Timeout = 60_000 });

            // التحقق من عدم وجود QR (session منتهية)
            var qr = await _page.QuerySelectorAsync("canvas[aria-label*='QR']");
            if (qr != null)
                throw new InvalidOperationException(
                    "WhatsApp session expired — QR code detected. " +
                    "Please scan manually and restart the service.");

            // انتظار شريط البحث كمؤشر على نجاح التحميل
            await _page.WaitForSelectorAsync(
                "div[data-testid='chat-list-search-container'], input[data-tab='3']",
                new WaitForSelectorOptions { Visible = true, Timeout = 30_000 });

            _logger.LogInformation("WhatsApp Web loaded successfully.");
        }

        // =====================================================================
        // Navigate — البحث عن الجروب والانتقال إليه مع التحقق
        // =====================================================================
        public async Task NavigateToChatAsync(string communityName, string subGroupName)
        {
            EnsureInitialized();
            _logger.LogInformation("Navigating to '{SubGroup}' in '{Community}'...", subGroupName, communityName);

            const int maxRetries = 3;
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                await ClearAndTypeSearchAsync(subGroupName);
                bool clicked = await TryClickChatResultAsync(communityName, subGroupName);

                if (!clicked)
                {
                    _logger.LogWarning("Smart click failed on attempt {A}, trying Enter...", attempt);
                    await _page!.Keyboard.PressAsync("Enter");
                    await Task.Delay(1500);
                }

                await Task.Delay(2000);

                string title = await _page!.EvaluateFunctionAsync<string>(JsGetChatTitle);
                _logger.LogInformation("Opened chat: '{Title}'", title);

                if (title.Contains(subGroupName, StringComparison.OrdinalIgnoreCase))
                    return; // نجح التنقل

                _logger.LogWarning(
                    "Attempt {A}/{Max}: Wrong chat '{Title}' instead of '{Expected}'.",
                    attempt, maxRetries, title, subGroupName);

                if (attempt == maxRetries)
                    throw new InvalidOperationException(
                        $"Failed after {maxRetries} attempts. Opened '{title}' instead of '{subGroupName}'.");
            }
        }

        // =====================================================================
        // Scrape — سحب الرسائل من الجروب الحالي
        // =====================================================================
        public async Task<IReadOnlyList<string>> ScrapeMessagesAsync(int daysToScrape)
        {
            EnsureInitialized();
            _logger.LogInformation("Scraping messages from the last {Days} days...", daysToScrape);

            await ScrollUpToDateBoundaryAsync(daysToScrape);
            return await HarvestMessagesAsync(daysToScrape);
        }

        // =====================================================================
        // Send — إرسال الملخص للجروب الحالي
        // =====================================================================
        public async Task SendMessageAsync(string message)
        {
            EnsureInitialized();

            var messageBox = await _page!.WaitForSelectorAsync(
                "div[data-testid='conversation-compose-box-input'], div[role='textbox'][data-tab='10']",
                new WaitForSelectorOptions { Visible = true, Timeout = 15_000 });

            await messageBox!.ClickAsync();
            await Task.Delay(500);

            // Paste عبر Clipboard API — أسرع بكثير من الكتابة حرف حرف
            await _page.EvaluateFunctionAsync(@"(text) => {
                const dt = new DataTransfer();
                dt.setData('text/plain', text);
                const ev = new ClipboardEvent('paste', { clipboardData: dt, bubbles: true });
                document.querySelector(""div[data-testid='conversation-compose-box-input']"")
                        .dispatchEvent(ev);
            }", message);

            await Task.Delay(1000);
            await messageBox.ClickAsync();
            await Task.Delay(500);
            await _page.Keyboard.PressAsync("Enter");
            await Task.Delay(1000);

            // Fallback: الضغط على زرار الإرسال لو Enter ما شتغلتش
            try
            {
                var sendBtn = await _page.QuerySelectorAsync(
                    "button[data-testid='compose-btn-send'], span[data-testid='send']");
                if (sendBtn != null)
                {
                    _logger.LogWarning("Enter didn't send — clicking Send button as fallback.");
                    await sendBtn.ClickAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Send button fallback skipped: {Msg}", ex.Message);
            }

            // انتظار تأكيد الإرسال من سيرفر واتساب
            await Task.Delay(20_000);
            _logger.LogInformation("Message sent successfully.");
        }

        // =====================================================================
        // IAsyncDisposable — إغلاق المتصفح وتحرير الموارد
        // =====================================================================
        public async ValueTask DisposeAsync()
        {
            if (_page != null)
            {
                await _page.CloseAsync();
                _page = null;
            }

            if (_browser != null)
            {
                await _browser.CloseAsync();
                await _browser.DisposeAsync();
                _browser = null;
            }
        }

        // =====================================================================
        // Private helpers
        // =====================================================================

        private void EnsureInitialized()
        {
            if (_page == null || _browser == null)
                throw new InvalidOperationException(
                    "WhatsAppClient is not initialized. Call InitializeAsync() first.");
        }

        private async Task ClearAndTypeSearchAsync(string query)
        {
            var searchBox = await _page!.WaitForSelectorAsync(
                "div[data-testid='chat-list-search-container'] div[role='textbox'], " +
                "div[data-testid='chat-list-search-container'] input, " +
                "input[data-tab='3']",
                new WaitForSelectorOptions { Visible = true, Timeout = 20_000 });

            await searchBox!.ClickAsync();
            await Task.Delay(800);

            // مسح محتوى الـ search box الحالي
            await _page.Keyboard.DownAsync("Control");
            await _page.Keyboard.PressAsync("A");
            await _page.Keyboard.UpAsync("Control");
            await _page.Keyboard.PressAsync("Backspace");
            await Task.Delay(500);

            await _page.Keyboard.TypeAsync(query);
            await Task.Delay(3500);
        }

        private async Task<bool> TryClickChatResultAsync(string communityName, string subGroupName)
        {
            return await _page!.EvaluateFunctionAsync<bool>(@"(comm, sub) => {
                const cleanComm = comm.trim().toLowerCase();
                const cleanSub  = sub.trim().toLowerCase();

                for (const container of document.querySelectorAll(""[data-testid='cell-frame-container']"")) {
                    const subSpan = container.querySelector(""span[title]"");
                    if (!subSpan) continue;
                    if (!subSpan.getAttribute('title').trim().toLowerCase().includes(cleanSub)) continue;

                    const labelEl = container.querySelector(""[data-testid='cell-frame-label'] span[title]"");
                    if (!labelEl) continue;
                    if (!labelEl.getAttribute('title').trim().toLowerCase().includes(cleanComm)) continue;

                    const target = container.closest(""[role='gridcell']"")
                                || container.closest(""[tabindex='0']"")
                                || container.parentElement;
                    target.click();
                    return true;
                }
                return false;
            }", communityName, subGroupName);
        }

        private async Task ScrollUpToDateBoundaryAsync(int daysToScrape)
        {
            const int maxScrolls = 30;

            // JS لا يستخدم C# string interpolation عشان الـ regex ما يتكسرش
            // بنمرر daysToScrape كـ argument مش كـ interpolated value
            const string scrollCheckJs = @"(days) => {
                const firstMsg = document.querySelector('.copyable-text[data-pre-plain-text]');
                if (!firstMsg) return false;
                const meta = firstMsg.getAttribute('data-pre-plain-text');
                if (!meta) return false;

                const m = meta.match(/(\d{2,4})[\/\-](\d{1,2})[\/\-](\d{1,2})/);
                if (!m) return false;
                const p = m[0].split(/[\/\-]/);
                let year, month, day;
                if (p[0].length === 4) {
                    year = parseInt(p[0]); month = parseInt(p[1]) - 1; day = parseInt(p[2]);
                } else {
                    year = p[2].length === 2 ? parseInt('20' + p[2]) : parseInt(p[2]);
                    month = parseInt(p[1]) - 1; day = parseInt(p[0]);
                }
                const msgDate = new Date(year, month, day);
                msgDate.setHours(0,0,0,0);
                const cutoff = new Date();
                cutoff.setDate(cutoff.getDate() - days);
                cutoff.setHours(0,0,0,0);
                return msgDate < cutoff;
            }";

            for (int i = 0; i < maxScrolls; i++)
            {
                await _page!.EvaluateFunctionAsync(
                    "() => { const el = document.querySelector(\"div[data-testid='conversation-panel-messages']\"); " +
                    "if (el) el.scrollBy(0, -1200); }");

                await Task.Delay(2000); // زيادة الانتظار عشان الرسائل تتحمل

                bool reachedLimit = await _page.EvaluateFunctionAsync<bool>(scrollCheckJs, daysToScrape);

                if (reachedLimit)
                {
                    _logger.LogInformation("Reached date boundary after {Count} scrolls.", i + 1);
                    break;
                }
            }
        }

        private async Task<IReadOnlyList<string>> HarvestMessagesAsync(int daysToScrape)
        {
            // ======================================================================
            // STEP 1: تشخيص — كم عنصر .copyable-text موجود في الصفحة أصلاً؟
            // ======================================================================
            int totalElements = await _page!.EvaluateFunctionAsync<int>(
                "() => document.querySelectorAll('.copyable-text').length");
            _logger.LogInformation("[Harvest] Total .copyable-text elements in DOM: {Count}", totalElements);

            // لو مافيش عناصر خالص → المشكلة في الـ selector مش في الفلتر
            if (totalElements == 0)
            {
                _logger.LogWarning("[Harvest] No .copyable-text elements found — wrong chat or messages not loaded yet.");
                return Array.Empty<string>();
            }

            // ======================================================================
            // STEP 2: جيب أول meta attribute موجود عشان تشوف الـ date format
            // ======================================================================
            string sampleMeta = await _page.EvaluateFunctionAsync<string>(@"() => {
                const el = document.querySelector('.copyable-text[data-pre-plain-text]');
                return el ? el.getAttribute('data-pre-plain-text') : 'NOT_FOUND';
            }");
            _logger.LogInformation("[Harvest] Sample meta attribute: {Meta}", sampleMeta);

            // ======================================================================
            // STEP 3: جيب الرسائل — JS كـ const string بدون interpolation
            // ======================================================================
            // مهم: الـ JS ده معزول تماماً عن C# interpolation
            // daysToScrape بيتمرر كـ argument للـ function مش كـ embedded string
            const string harvestJs = @"(days) => {
                function parseMetaDate(meta) {
                    const m = meta.match(/(\d{2,4})[\/\-](\d{1,2})[\/\-](\d{1,2})/);
                    if (!m) return null;
                    const p = m[0].split(/[\/\-]/);
                    let year, month, day;
                    if (p[0].length === 4) {
                        year = parseInt(p[0]); month = parseInt(p[1]) - 1; day = parseInt(p[2]);
                    } else {
                        year = p[2].length === 2 ? parseInt('20' + p[2]) : parseInt(p[2]);
                        month = parseInt(p[1]) - 1; day = parseInt(p[0]);
                    }
                    const dt = new Date(year, month, day);
                    dt.setHours(0,0,0,0);
                    return dt;
                }

                const cutoff = new Date();
                cutoff.setDate(cutoff.getDate() - days);
                cutoff.setHours(0,0,0,0);

                const results   = [];
                const skipped   = [];
                const noDate    = [];

                document.querySelectorAll('.copyable-text').forEach(el => {
                    // استبعاد الميديا
                    if (el.querySelector('img[src^=""blob:""]') ||
                        el.querySelector('audio') ||
                        el.closest('[data-testid=""sticker-container""]')) {
                        skipped.push('media');
                        return;
                    }

                    const text = (el.innerText || el.textContent || '').trim();

                    // استبعاد النص الفاضي
                    if (!text) { skipped.push('empty'); return; }

                    // استبعاد نصوص مجرد timestamp للريكورد (مثل ""0:15"")
                    if (/^\d{1,2}:\d{2}$/.test(text)) { skipped.push('timestamp'); return; }

                    const meta = el.getAttribute('data-pre-plain-text');

                    // رسالة بدون meta (continuation message) — نضيفها لو عندنا رسائل
                    if (!meta) {
                        if (results.length > 0) results.push(text);
                        noDate.push(text.substring(0, 30));
                        return;
                    }

                    const msgDate = parseMetaDate(meta);

                    // لو الـ date مش اتعرفت — نضيفها احتياطاً ونشوفها في اللوج
                    if (!msgDate) {
                        noDate.push(meta);
                        results.push('[NO_DATE] ' + meta + text);
                        return;
                    }

                    if (msgDate >= cutoff) {
                        results.push(meta + text);
                    } else {
                        skipped.push('old:' + msgDate.toISOString().substring(0,10));
                    }
                });

                // نرجع object فيه النتائج والتشخيص
                return {
                    messages : results,
                    skipped  : skipped.length,
                    noDate   : noDate.length,
                    total    : results.length + skipped.length
                };
            }";

            var result = await _page.EvaluateFunctionAsync<HarvestResult>(harvestJs, daysToScrape);

            _logger.LogInformation(
                "[Harvest] Results: {Msgs} messages | {Skipped} skipped | {NoDate} no-date | {Total} total seen",
                result.Messages.Length, result.Skipped, result.NoDate, result.Total);

            // لو الفلتر بالتاريخ قتل كل الرسائل → ارجع بدون فلتر كـ fallback مع تحذير
            if (result.Messages.Length == 0 && totalElements > 0)
            {
                _logger.LogWarning(
                    "[Harvest] Date filter returned 0 — falling back to all visible messages (check date format in sample meta above).");

                string[] fallback = await _page.EvaluateFunctionAsync<string[]>(@"() =>
                    Array.from(document.querySelectorAll('.copyable-text'))
                         .map(el => (el.innerText || el.textContent || '').trim())
                         .filter(t => t.length > 3 && !/^\d{1,2}:\d{2}$/.test(t))
                ");

                _logger.LogInformation("[Harvest] Fallback collected {Count} messages.", fallback.Length);
                return fallback.ToList().AsReadOnly();
            }

            return result.Messages.ToList().AsReadOnly();
        }

        // DTO للنتيجة من الـ JS — بنستخدمها بدل string[] عشان نجيب التشخيص معاها
        private sealed class HarvestResult
        {
            public string[] Messages { get; set; } = Array.Empty<string>();
            public int Skipped { get; set; }
            public int NoDate { get; set; }
            public int Total { get; set; }
        }
    }
}