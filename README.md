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
- [Architecture](#-architecture)
- [Key Methods](#-key-methods)
- [Important Notes](#-important-notes--limitations)
- [Troubleshooting](#-troubleshooting)

---

## 💡 What is this project?

**WhatsApp AI Automation** is a **.NET 8 Background Service** that:

1. ⏰ Wakes up every day at a **scheduled time**
2. 🌐 Opens **WhatsApp Web** automatically using Microsoft Edge
3. 📥 **Scrapes** messages from a source community sub-group based on a configurable number of days
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
📥 Navigate to Source Group → Scroll up → Scrape Messages
        │
        ▼
🔍 Filter & validate messages (date, media, text)
        │
        ▼
🧠 Send to Gemini API (with multi-model fallback) → Get Arabic Summary
        │
        ▼
📤 Navigate to Target Group → Paste & Send Summary
        │
        ▼
🔒 Dispose Browser → Wait for next day
```

| Step | What happens |
|------|-------------|
| 1 — Wait for schedule | Calculates next run time from `appsettings.json` and sleeps until then |
| 2 — Launch Edge | `WhatsAppClient` launches Microsoft Edge with a saved session to skip QR |
| 3 — Load WhatsApp Web | Navigates to `web.whatsapp.com`, detects QR screen, waits for full load |
| 4 — Scrape source group | Searches for the source sub-group, scrolls up dynamically until the date boundary, extracts messages via JavaScript |
| 5 — Filter messages | Removes media, stickers, audio timestamps, and messages older than `DaysToScrape` |
| 6 — Call Gemini AI | Sends filtered messages to Gemini API with a structured Arabic prompt — tries up to 5 models automatically |
| 7 — Format summary | Wraps AI response in a header with message count and timestamp |
| 8 — Send to target group | Navigates to target sub-group and pastes summary using a Clipboard event |
| 9 — Dispose browser | Closes page and browser properly (`IAsyncDisposable`) and waits for next scheduled run |

---

## 🛠️ Prerequisites

### Operating System
> ⚠️ **Windows 10 / 11 (64-bit) only** — Edge path defaults to a Windows directory (configurable in `appsettings.json`).

### Runtime

| Requirement | Details |
|-------------|---------|
| **.NET 8 SDK** | [Download here](https://dotnet.microsoft.com/download/dotnet/8.0) |
| **Microsoft Edge** | Must be installed at `C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe` (or configure custom path) |
| **WhatsApp Account** | A valid account that has scanned the QR code at least once |

### NuGet Packages
> Auto-restored by `dotnet restore` — no manual install needed.

| Package | Purpose |
|---------|---------|
| `PuppeteerSharp` | Controls Microsoft Edge browser programmatically |
| `Microsoft.Extensions.Hosting` | Provides `BackgroundService` base class and lifecycle |
| `Microsoft.Extensions.Http` | Provides `IHttpClientFactory` for safe HTTP client management |
| `Microsoft.Extensions.Configuration` | Reads settings from `appsettings.json` |
| `Microsoft.Extensions.Logging` | Structured logging (Info / Warning / Error / Critical) |
| `System.Text.Json` | Built-in .NET JSON serialization |

### External APIs

| API | Details |
|-----|---------|
| **Google Gemini AI** | Free tier available. Get your key from [Google AI Studio](https://aistudio.google.com/apikey). Key must start with `AIzaSy...` |
| **WhatsApp Web** | No official API — the bot automates the browser UI directly via PuppeteerSharp |

---

## 🚀 Installation & Setup

### Step 1 — Clone the repository
```bash
git clone https://github.com/bedoax/WebScrapSummeryWhatsAppWeb.git
cd WebScrapSummeryWhatsAppWeb
```

### Step 2 — Restore NuGet packages
```bash
dotnet restore
```

### Step 3 — Create `appsettings.json`
> ⚠️ This file is excluded from Git for security. Create it manually:

```json
{
  "WhatsAppAutomation": {
    "CommunityName":  "YourCommunityName",
    "SourceSubGroup": "General",
    "TargetSubGroup": "Resources",
    "GeminiApiKey":   "AIzaSy_YOUR_KEY_HERE",
    "RunHour":        "19",
    "RunMinute":      "00",
    "DaysToScrape":   "1"
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
| `CommunityName` | `CognitionX` | Name of the WhatsApp community |
| `SourceSubGroup` | `General` | Exact name of the sub-group to scrape messages from |
| `TargetSubGroup` | `Resources` | Exact name of the sub-group to post the summary into |
| `GeminiApiKey` | *(required)* | Your Google Gemini API key — must start with `AIzaSy` — never commit this! |
| `RunHour` | `19` | Hour (24h format) at which the pipeline runs daily |
| `RunMinute` | `00` | Minute at which the pipeline runs daily |
| `DaysToScrape` | `1` | How many days back to scrape messages (1 = today only, 7 = last week) |
| `EdgePath` | `C:\...\msedge.exe` | Optional: override the default Edge executable path |

---

## 📁 Project Structure

```
WebScrapSummeryWhatsAppWeb/
│
├── Interfaces/
│   ├── IWhatsAppClient.cs     ← Contract for all browser operations
│   ├── IGeminiService.cs      ← Contract for AI summarization
│   └── IMessageFilter.cs      ← Contract for text filtering
│
├── Services/
│   ├── WhatsAppAutomationWorker.cs  ← Scheduler + pipeline coordinator only
│   ├── WhatsAppClient.cs            ← All browser logic (navigate, scrape, send)
│   └── GeminiService.cs             ← Gemini API calls with multi-model fallback
│
├── Filters/
│   └── GeminiMessageFilter.cs  ← Trims text to 40,000 chars max
│
├── Program.cs                  ← DI registration for all services
├── appsettings.example.json    ← Template — copy and fill in your values
│
└── WhatsAppUserData/           ← Auto-created: stores WhatsApp browser session
    └── (browser session files — excluded from Git)
```

---

## 🏗️ Architecture

The project follows **Separation of Concerns** with clearly defined interfaces:

```
WhatsAppAutomationWorker
  │  (Scheduler + Pipeline coordinator)
  │
  ├── IWhatsAppClient  →  WhatsAppClient
  │     ├── InitializeAsync()       — Launch Edge, load WhatsApp Web
  │     ├── NavigateToChatAsync()   — Search and open a group (with retry)
  │     ├── ScrapeMessagesAsync()   — Scroll + harvest messages by date
  │     └── SendMessageAsync()      — Paste and send via Clipboard API
  │
  ├── IGeminiService   →  GeminiService
  │     └── SummarizeAsync()        — Call Gemini with 5-model fallback chain
  │
  └── IMessageFilter   →  GeminiMessageFilter
        └── Filter()                — Trim to last 40,000 characters
```

### Gemini Model Fallback Chain

If a model is unavailable or rate-limited, the service automatically tries the next:

| Priority | Model | Notes |
|----------|-------|-------|
| Primary | `gemini-3.5-flash` | Latest — requires billing |
| Secondary | `gemini-3.1-flash-lite` | Fast & lightweight — requires billing |
| Tertiary | `gemini-2.5-flash` | Stable — free tier available |
| Quaternary | `gemini-2.5-flash-lite` | Lightest — free tier available |
| Fallback | `gemini-2.0-flash` | Most widely available |

---

## 🔧 Key Methods

| Class | Method | Description |
|-------|--------|-------------|
| `WhatsAppAutomationWorker` | `ExecuteAsync()` | Main loop — schedules and triggers the daily pipeline |
| `WhatsAppAutomationWorker` | `RunPipelineAsync()` | Coordinates all 4 pipeline steps in order |
| `WhatsAppClient` | `InitializeAsync()` | Launches Edge, loads WhatsApp Web, detects QR expiry |
| `WhatsAppClient` | `NavigateToChatAsync()` | Searches for a group with up to 3 retry attempts |
| `WhatsAppClient` | `ScrapeMessagesAsync()` | Scrolls up to date boundary then harvests filtered messages |
| `WhatsAppClient` | `SendMessageAsync()` | Pastes summary via Clipboard event + Enter fallback |
| `GeminiService` | `SummarizeAsync()` | Loops through model chain until one succeeds |
| `GeminiMessageFilter` | `Filter()` | Trims to last 40,000 characters (newest messages kept) |

---

## ⚠️ Important Notes & Limitations

- 🪟 **Windows only** — Edge path defaults to Windows. Linux/Mac require updating `EdgePath` in config.
- 📜 **WhatsApp Web ToS** — Automating WhatsApp Web may violate WhatsApp's Terms of Service. Use responsibly.
- ✂️ **Gemini token limit** — Messages trimmed to last 40,000 characters. Older messages may be cut off.
- 🔄 **Selectors may break** — WhatsApp Web updates its HTML periodically. CSS selectors may need updating.
- 📅 **One run per day** — The scheduler runs the pipeline exactly once per day at the configured time.
- 🔑 **API Key** — Must start with `AIzaSy`. Keys from other Google services will return 401.
- 💳 **Free tier limits** — `gemini-3.5-flash` and `gemini-3.1-flash-lite` require billing. The fallback chain ensures the service still works on free tier using `gemini-2.5-flash`.

---

## 🐛 Troubleshooting

| Problem | Solution |
|---------|----------|
| Edge not found | Verify Edge is installed, or set `EdgePath` in `appsettings.json` |
| QR code appears every time | Make sure `WhatsAppUserData/` is not deleted between runs |
| Gemini returns 401 Unauthorized | API key is wrong or from the wrong service — must start with `AIzaSy` |
| Gemini returns 429 TooManyRequests | Free tier rate limit hit — the fallback chain will try the next model automatically |
| Gemini returns 404 NotFound | Model name is incorrect or not available in your region |
| 0 messages scraped | Check logs for `[Harvest] Sample meta attribute` — shows the date format WhatsApp is using |
| Wrong chat opened | Group name in config doesn't match exactly — check for extra spaces or Arabic vs English characters |
| Message not sent | Increase `Task.Delay` in `SendMessageAsync` (currently 20 seconds) |
| Graceful shutdown logs as Critical | Normal — pressing Ctrl+C triggers `OperationCanceledException` which is caught cleanly |

---

## 🏗️ Built With

![.NET](https://img.shields.io/badge/.NET_8-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)
![C#](https://img.shields.io/badge/C%23-239120?style=for-the-badge&logo=csharp&logoColor=white)
![Google Gemini](https://img.shields.io/badge/Google_Gemini-4285F4?style=for-the-badge&logo=google&logoColor=white)
![WhatsApp](https://img.shields.io/badge/WhatsApp_Web-25D366?style=for-the-badge&logo=whatsapp&logoColor=white)
![Edge](https://img.shields.io/badge/Microsoft_Edge-0078D7?style=for-the-badge&logo=microsoft-edge&logoColor=white)

---

> ⭐ If this project helped you, consider giving it a star!
