namespace AutomationDemo.Interfaces
{
    namespace AutomationDemo.Interfaces
    {
        /// <summary>
        /// تصفية وتنظيف النصوص قبل إرسالها للـ AI
        /// </summary>
        public interface IMessageFilter
        {
            /// <summary>
            /// تصفية النص وضمان أنه ضمن الحد الأقصى المسموح به
            /// </summary>
            string Filter(string text);
        }
    }
}
