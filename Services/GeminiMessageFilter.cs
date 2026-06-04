using AutomationDemo.Interfaces;
using AutomationDemo.Interfaces.AutomationDemo.Interfaces;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace AutomationDemo.Services
{

    namespace AutomationDemo.Filters
    {
        /// <summary>
        /// تصفية النصوص لضمان التوافق مع حدود Gemini API (40,000 حرف كحد أقصى)
        /// </summary>
        public class GeminiMessageFilter : IMessageFilter
        {
            private const int MaxCharacters = 40_000;

            /// <summary>
            /// يُرجع آخر MaxCharacters حرف من النص (الأحدث أهم للتلخيص)
            /// </summary>
            public string Filter(string text)
            {
                if (string.IsNullOrEmpty(text))
                    return string.Empty;

                return text.Length > MaxCharacters
                    ? text[^MaxCharacters..]   // C# 8 range syntax بدل Substring
                    : text;
            }
        }
    }
}