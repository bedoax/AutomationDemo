using AutomationDemo.Interfaces;
using AutomationDemo.Interfaces.AutomationDemo.Interfaces;
using AutomationDemo.Services;
using AutomationDemo.Services.AutomationDemo.Filters;


var builder = WebApplication.CreateBuilder(args);


builder.Services.AddHttpClient(nameof(GeminiService));


builder.Services.AddSingleton<IMessageFilter, GeminiMessageFilter>();


builder.Services.AddTransient<IWhatsAppClient, WhatsAppClient>();


builder.Services.AddSingleton<IGeminiService, GeminiService>();

builder.Services.AddHostedService<WhatsAppAutomationWorker>();

var app = builder.Build();
app.Run();
