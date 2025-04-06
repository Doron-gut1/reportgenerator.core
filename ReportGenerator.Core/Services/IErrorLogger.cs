using System;
using ReportGenerator.Core.Errors;

namespace ReportGenerator.Core.Services
{
    /// <summary>
    /// ממשק לרישום שגיאות במערכת
    /// </summary>
    public interface IErrorLogger
    {
        /// <summary>
        /// רושם שגיאה במערכת
        /// </summary>
        /// <param name="errorCode">קוד השגיאה</param>
        /// <param name="severity">חומרת השגיאה</param>
        /// <param name="message">הודעת השגיאה</param>
        /// <param name="ex">חריגה מקורית (אופציונלי)</param>
        /// <param name="reportName">שם הדוח (אופציונלי)</param>
        /// <returns>האם אפשר להמשיך בתהליך</returns>
        bool LogError(string errorCode, ErrorSeverity severity, string message, Exception ex = null, string reportName = null);

        /// <summary>
        /// רושם אזהרה במערכת
        /// </summary>
        /// <param name="errorCode">קוד השגיאה</param>
        /// <param name="message">הודעת האזהרה</param>
        /// <param name="ex">חריגה מקורית (אופציונלי)</param>
        /// <param name="reportName">שם הדוח (אופציונלי)</param>
        void LogWarning(string errorCode, string message, Exception ex = null, string reportName = null);

        /// <summary>
        /// רושם מידע במערכת
        /// </summary>
        /// <param name="errorCode">קוד ההודעה</param>
        /// <param name="message">הודעת המידע</param>
        /// <param name="reportName">שם הדוח (אופציונלי)</param>
        void LogInfo(string errorCode, string message, string reportName = null);

        /// <summary>
        /// רושם שגיאה קריטית במערכת
        /// </summary>
        /// <param name="errorCode">קוד השגיאה</param>
        /// <param name="message">הודעת השגיאה</param>
        /// <param name="ex">חריגה מקורית (אופציונלי)</param>
        /// <param name="reportName">שם הדוח (אופציונלי)</param>
        /// <returns>האם אפשר להמשיך בתהליך</returns>
        bool LogCriticalError(string errorCode, string message, Exception ex = null, string reportName = null);

        /// <summary>
        /// רושם שגיאה רגילה במערכת
        /// </summary>
        /// <param name="errorCode">קוד השגיאה</param>
        /// <param name="message">הודעת השגיאה</param>
        /// <param name="ex">חריגה מקורית (אופציונלי)</param>
        /// <param name="reportName">שם הדוח (אופציונלי)</param>
        /// <returns>האם אפשר להמשיך בתהליך</returns>
        bool LogNormalError(string errorCode, string message, Exception ex = null, string reportName = null);

        /// <summary>
        /// מנקה רשימת שגיאות
        /// </summary>
        void ClearErrors();
    }
}
