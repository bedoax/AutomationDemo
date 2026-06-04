namespace AutomationDemo.Interfaces
{
    /// <summary>
    /// خدمة التلخيص الذكي باستخدام Gemini API
    /// </summary>
    public interface IGeminiService
    {
        /// <summary>
        /// تلخيص نص باستخدام Gemini مع fallback تلقائي للموديلات الاحتياطية
        /// </summary>
        Task<string> SummarizeAsync(string text);
    }
}
