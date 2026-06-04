using AutomationDemo.Interfaces;
using AutomationDemo.Interfaces.AutomationDemo.Interfaces;
using AutomationDemo.Services;
using AutomationDemo.Services.AutomationDemo.Filters;


var builder = WebApplication.CreateBuilder(args);

// =====================================================================
// تسجيل الـ services في الـ DI Container
// =====================================================================

// IHttpClientFactory بدل new HttpClient() — يحل مشكلة socket exhaustion
builder.Services.AddHttpClient(nameof(GeminiService));

// Filters
builder.Services.AddSingleton<IMessageFilter, GeminiMessageFilter>();

// WhatsApp client — Transient لأنه يُنشأ ويُتخلص منه في كل pipeline run
builder.Services.AddTransient<IWhatsAppClient, WhatsAppClient>();

// Gemini — Singleton لأنه stateless ويشارك HttpClient واحد
builder.Services.AddSingleton<IGeminiService, GeminiService>();

// Worker الرئيسي
builder.Services.AddHostedService<WhatsAppAutomationWorker>();

var app = builder.Build();
app.Run();