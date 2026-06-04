namespace AutomationDemo.Interfaces
{
    public interface IWhatsAppClient : IAsyncDisposable
    {
        /// <summary>
        /// فتح المتصفح وتسجيل الدخول لواتساب ويب
        /// </summary>
        Task InitializeAsync();

        /// <summary>
        /// التنقل لجروب معين داخل كوميونيتي
        /// </summary>
        Task NavigateToChatAsync(string communityName, string subGroupName);

        /// <summary>
        /// سحب الرسائل من الجروب الحالي بناءً على عدد الأيام
        /// </summary>
        Task<IReadOnlyList<string>> ScrapeMessagesAsync(int daysToScrape);

        /// <summary>
        /// إرسال رسالة للجروب الحالي
        /// </summary>
        Task SendMessageAsync(string message);
    }
}
