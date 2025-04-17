using System.Collections.Generic;
using System.Data;

namespace ReportGenerator.Core.Interfaces
{
    /// <summary>
    /// ממשק לעיבוד תבניות HTML
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
    }
}
