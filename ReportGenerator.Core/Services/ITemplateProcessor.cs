using System.Collections.Generic;
using System.Data;

namespace ReportGenerator.Core.Services
{
    /// <summary>
    /// ממשק למעבד תבניות
    /// </summary>
    public interface ITemplateProcessor
    {
        /// <summary>
        /// מעבד תבנית HTML והחלפת כל הפלייסהולדרים בערכים
        /// </summary>
        /// <param name="template">תבנית HTML</param>
        /// <param name="values">מילון ערכים לפלייסהולדרים פשוטים</param>
        /// <param name="dataTables">טבלאות נתונים לפלייסהולדרים מורכבים</param>
        /// <returns>HTML מעובד עם ערכים אמיתיים</returns>
        string ProcessTemplate(
                 string template,
                 Dictionary<string, object> values,
                 Dictionary<string, DataTable> dataTables);
        
        /// <summary>
        /// ניקוי המטמון
        /// </summary>
        void ClearCache();
        
        /// <summary>
        /// פורמט ערכים לתצוגה
        /// </summary>
        static string FormatValue(object value);
    }
}