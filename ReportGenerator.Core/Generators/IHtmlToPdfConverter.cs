using System.Threading.Tasks;

namespace ReportGenerator.Core.Generators
{
    /// <summary>
    /// ממשק לממירי HTML ל-PDF
    /// </summary>
    public interface IHtmlToPdfConverter
    {
        /// <summary>
        /// ממיר תוכן HTML ל-PDF
        /// </summary>
        /// <param name="html">תוכן HTML</param>
        /// <param name="title">כותרת המסמך</param>
        /// <returns>מערך בייטים של PDF</returns>
        Task<byte[]> ConvertToPdf(string html, string title = null);

        /// <summary>
        /// ממיר HTML ל-PDF עם כותרות מותאמות
        /// </summary>
        /// <param name="html">תוכן HTML</param>
        /// <param name="title">כותרת המסמך</param>
        /// <param name="headerHtml">HTML לכותרת עליונה</param>
        /// <param name="footerHtml">HTML לכותרת תחתונה</param>
        /// <returns>מערך בייטים של PDF</returns>
        Task<byte[]> ConvertToPdfWithCustomHeaderFooter(string html, string title, string headerHtml, string footerHtml);

        /// <summary>
        /// חילוץ חלק הכותרת העליונה מתבנית HTML
        /// </summary>
        /// <param name="html">תוכן HTML</param>
        /// <returns>HTML של הכותרת העליונה</returns>
        string ExtractHeaderFragment(string html);

        /// <summary>
        /// חילוץ חלק הכותרת התחתונה מתבנית HTML
        /// </summary>
        /// <param name="html">תוכן HTML</param>
        /// <returns>HTML של הכותרת התחתונה</returns>
        string ExtractFooterFragment(string html);
    }
}