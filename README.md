# 🤖 WhatsApp AI Automation
### Daily Group Summarizer — Powered by Google Gemini AI

> خدمة .NET 8 بتشتغل في الخلفية كل يوم، بتسحب رسائل جروب واتساب تلقائياً، تبعتها لـ Gemini AI، وتنشر الملخص في جروب تاني — من غير أي تدخل يدوي.

---

## 📋 Table of Contents

- [What is this project?](#-what-is-this-project)
- [How it works](#-how-it-works)
- [Prerequisites](#-prerequisites)
- [Installation & Setup](#-installation--setup)
- [Configuration Reference](#-configuration-reference)
- [Project Structure](#-project-structure)
- [Key Methods](#-key-methods)
- [Important Notes](#-important-notes--limitations)
- [Troubleshooting](#-troubleshooting)

---

## 💡 What is this project?

**WhatsApp AI Automation** is a **.NET 8 Background Service** that:

1. ⏰ Wakes up every day at a **scheduled time**
2. 🌐 Opens **WhatsApp Web** automatically using Microsoft Edge
3. 📥 **Scrapes** all messages from a source community sub-group
4. 🧠 Sends them to **Google Gemini AI** for intelligent Arabic summarization
5. 📤 Posts the formatted summary into a **target sub-group**

All without any manual interaction.

---

## ⚙️ How it works

```
⏰ Scheduler wakes up
        │
        ▼
🌐 Launch Edge + Load WhatsApp Web
        │
        ▼
📥 Navigate to Source Group → Scrape Messages
        │
        ▼
🧠 Send to Gemini API → Get Arabic Summary
        │
        ▼
📤 Navigate to Target Group → Paste & Send Summary
        │
        ▼
🔒 Close Browser → Wait for next day
```

| Step | What happens |
|------|-------------|
| 1 — Wait for schedule | Calculates next run time from `appsettings.json` and sleeps until then |
| 2 — Launch Edge | PuppeteerSharp launches Microsoft Edge using the saved WhatsApp session |
| 3 — Load WhatsApp Web | Navigates to `web.whatsapp.com`, waits for full initialization |
| 4 — Scrape source group | Searches for the source sub-group, scrolls up 5× to load older messages, extracts all messages via JavaScript |
| 5 — Call Gemini AI | Sends messages to Gemini API with a structured Arabic prompt |
| 6 — Format summary | Wraps AI response in a branded header with message count and timestamp |
| 7 — Send to target group | Navigates to target sub-group and pastes summary using a Clipboard event |
| 8 — Close browser | Closes Edge cleanly and waits for the next scheduled run |

---

## 🛠️ Prerequisites

### Operating System
> ⚠️ **Windows 10 / 11 (64-bit) only** — Edge path is hardcoded to a Windows directory.

### Runtime

| Requirement | Details |
|-------------|---------|
| **.NET 8 SDK** | [Download here](https://dotnet.microsoft.com/download/dotnet/8.0) |
| **Microsoft Edge** | Must be installed at `C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe` |
| **WhatsApp Account** | A valid account that has scanned the QR code at least once |

### NuGet Packages
> These are auto-restored by `dotnet restore` — no manual install needed.

| Package | Purpose |
|---------|---------|
| `PuppeteerSharp` | Controls Microsoft Edge browser programmatically |
| `Microsoft.Extensions.Hosting` | Provides `BackgroundService` base class and lifecycle |
| `Microsoft.Extensions.Configuration` | Reads settings from `appsettings.json` |
| `Microsoft.Extensions.Logging` | Structured logging (Info / Warning / Error / Critical) |
| `System.Text.Json` | Built-in .NET JSON serialization — no extra package needed |
| `System.Net.Http` | Built-in .NET HTTP client — no extra package needed |

### External APIs

| API | Details |
|-----|---------|
| **Google Gemini AI** | Free tier available. Get your key from [Google AI Studio](https://aistudio.google.com/app/apikey) — Model: `gemini-3.5-flash` |
| **WhatsApp Web** | No official API — the bot automates the browser UI directly via PuppeteerSharp |

---

## 🚀 Installation & Setup

### Step 1 — Clone the repository
```bash
git clone https://github.com/bedoax/AutomationDemo.git
cd AutomationDemo
```

### Step 2 — Restore NuGet packages
```bash
dotnet restore
```

### Step 3 — Configure `appsettings.json`
```json
{
  "WhatsAppAutomation": {
    "CommunityName":  "YourCommunityName",
    "SourceSubGroup": "General",
    "TargetSubGroup": "Resources",
    "GeminiApiKey":   "YOUR_GEMINI_API_KEY_HERE",
    "RunHour":        "19",
    "RunMinute":      "49"
  }
}
```

### Step 4 — First-time WhatsApp login
- Run the project once manually
- Edge will open WhatsApp Web and show a **QR code**
- Scan it with your phone
- The session is saved in `WhatsAppUserData/` — **QR code won't appear again**

### Step 5 — Run the service
```bash
dotnet run
```

The service logs the next scheduled run time and waits automatically.

---

## 📝 Configuration Reference

| Key | Default | Description |
|-----|---------|-------------|
| `CommunityName` | `CognitionX` | Name of the WhatsApp community (for logging only) |
| `SourceSubGroup` | `General` | Exact name of the sub-group to scrape messages from |
| `TargetSubGroup` | `Resources` | Exact name of the sub-group to post the summary into |
| `GeminiApiKey` | *(required)* | Your Google Gemini API key — never commit this! |
| `RunHour` | `19` | Hour (24h format) at which the pipeline runs daily |
| `RunMinute` | `49` | Minute at which the pipeline runs daily |

---

## 📁 Project Structure

```
AutomationDemo/
│
├── Services/
│   └── WhatsAppAutomationWorker.cs   ← Main automation pipeline
│
├── appsettings.json                  ← All runtime configuration
├── Program.cs                        ← Host builder & service registration
│
└── WhatsAppUserData/                 ← Auto-created: stores WhatsApp session
    └── (browser session files)
```

---

## 🔧 Key Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `ExecuteAsync()` | `Task` | Main loop — schedules and runs the daily pipeline |
| `LoadConfigurations()` | `void` | Reads `appsettings.json` and refreshes all settings each cycle |
| `NavigateToChatAsync(name)` | `Task` | Searches WhatsApp for a sub-group by name and opens it |
| `NavigateAndScrapGroupAsync()` | `List<string>` | Scrolls up 5× for lazy-load then extracts all messages |
| `CallGeminiToSummarizeAsync()` | `string` | Posts messages to Gemini API, returns Arabic summary |
| `chimneysFilter(text)` | `string` | Trims text to last 40,000 chars to stay within Gemini limits |
| `NavigateAndSendSummaryAsync()` | `Task` | Pastes summary into target group using Clipboard event |
| `StopAsync()` | `Task` | Gracefully closes Edge and disposes `HttpClient` on shutdown |

---

## ⚠️ Important Notes & Limitations

- 🪟 **Windows only** — Edge path is hardcoded. Linux/Mac require path changes .
- 📜 **WhatsApp Web ToS** — Automating WhatsApp Web may violate WhatsApp's Terms of Service. Use responsibly.
- ✂️ **Gemini token limit** — Messages trimmed to last 40,000 characters. Older messages may be cut off.
- 🔄 **Selectors may break** — WhatsApp Web updates its HTML periodically. CSS selectors may need updating.
- 📅 **One run per day** — The scheduler runs the pipeline exactly once per day at the configured time.

---

## 🐛 Troubleshooting

| Problem | Solution |
|---------|----------|
| Edge not found error | Verify Edge is installed at the exact hardcoded path, or update `edgePath` |
| QR code appears every time | Make sure `WhatsAppUserData/` folder is not deleted between runs |
| Gemini API returns 400/403 | Check that `GeminiApiKey` is correct and has quota remaining |
| No messages scraped | The CSS selector `.copyable-text` may have changed — inspect WhatsApp Web DOM and update |
| Message not sent | Both Enter key and green send button are tried automatically — check logs for details |
| Timeout waiting for WhatsApp | Increase `Timeout` values in `WaitForSelectorOptions`, or check internet connection |

---

## 🏗️ Built With

![.NET](https://img.shields.io/badge/.NET_8-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)
![C#](https://img.shields.io/badge/C%23-239120?style=for-the-badge&logo=csharp&logoColor=white)
![Google Gemini](https://img.shields.io/badge/Google_Gemini-4285F4?style=for-the-badge&logo=google&logoColor=white)
![WhatsApp](https://img.shields.io/badge/WhatsApp_Web-25D366?style=for-the-badge&logo=whatsapp&logoColor=white)
![Edge](https://img.shields.io/badge/Microsoft_Edge-0078D7?style=for-the-badge&logo=microsoft-edge&logoColor=white)

---

> ⭐ If this project helped you, consider giving it a star!
