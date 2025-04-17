using System;
using System.Runtime.CompilerServices;
using ReportGenerator.Core.Errors;

namespace ReportGenerator.Core.Interfaces
{
    /// <summary>
    /// ממשק לניהול שגיאות
    /// </summary>
    public interface IErrorManager
    {
        /// <summary>
        /// רף החומרה לרישום שגיאות לDB
        /// </summary>
        ErrorSeverity LogThreshold { get; set; }
        
        /// <summary>
        /// רף החומרה להפסקת תהליך
        /// </summary>
        ErrorSeverity BreakThreshold { get; set; }
        
        /// <summary>
        /// מספר השגיאות שנרשמו
        /// </summary>
        int ErrorCount { get; }
        
        /// <summary>
        /// רישום השגיאה האחרונה
        /// </summary>
        ErrorContext LastError { get; }

        /// <summary>
        /// מאפס את כל הנתונים של מערכת השגיאות
        /// </summary>
        void Reset();
        
        /// <summary>
        /// קבלת השגיאה האחרונה מסוג חומרה ספציפי
        /// </summary>
        /// <param name="severity">סוג החומרה</param>
        /// <returns>אובייקט השגיאה או null</returns>
        ErrorContext? GetLastErrorByType(ErrorSeverity severity);

        /// <summary>
        /// טיפול בשגיאה וקבלת החלטה אם להמשיך
        /// </summary>
        /// <param name="error">אובייקט שגיאה</param>
        /// <returns>האם ניתן להמשיך בתהליך</returns>
        bool HandleError(ErrorContext error);
        
        /// <summary>
        /// ניקוי רשימת השגיאות (בין הפקות דוחות)
        /// </summary>
        void ClearErrors();

        /// <summary>
        /// יצירת שגיאה ושליחתה לטיפול
        /// </summary>
        /// <param name="errorCode">קוד שגיאה</param>
        /// <param name="severity">רמת חומרה</param>
        /// <param name="description">תיאור השגיאה</param>
        /// <param name="ex">החריגה המקורית (אופציונלי)</param>
        /// <param name="reportName">שם הדוח (אופציונלי)</param>
        /// <param name="jobNumber">מספר הפקת דוח (אופציונלי)</param>
        /// <param name="methodName">שם המתודה (אוטומטי)</param>
        /// <param name="filePath">נתיב הקובץ (אוטומטי)</param>
        /// <param name="lineNumber">מספר השורה (אוטומטי)</param>
        /// <returns>האם ניתן להמשיך בתהליך</returns>
        bool LogError(
            ErrorCode errorCode,
            ErrorSeverity severity,
            string description,
            Exception? ex = null,
            string? reportName = null,
            int jobNumber = 0,
            [CallerMemberName] string? methodName = null,
            [CallerFilePath] string? filePath = null,
            [CallerLineNumber] int lineNumber = 0);
        
        /// <summary>
        /// רישום שגיאה מסוג מידע
        /// </summary>
        bool LogInfo(
            ErrorCode errorCode,
            string description,
            Exception? ex = null,
            string? reportName = null,
            int jobNumber = 0,
            [CallerMemberName] string? methodName = null,
            [CallerFilePath] string? filePath = null,
            [CallerLineNumber] int lineNumber = 0);
        
        /// <summary>
        /// רישום שגיאה מסוג אזהרה
        /// </summary>
        bool LogWarning(
            ErrorCode errorCode,
            string description,
            Exception? ex = null,
            string? reportName = null,
            int jobNumber = 0,
            [CallerMemberName] string? methodName = null,
            [CallerFilePath] string? filePath = null,
            [CallerLineNumber] int lineNumber = 0);

        /// <summary>
        /// רישום שגיאה רגילה
        /// </summary>
        bool LogNormalError(
            ErrorCode errorCode,
            string description,
            Exception? ex = null,
            string? reportName = null,
            int jobNumber = 0,
            [CallerMemberName] string? methodName = null,
            [CallerFilePath] string? filePath = null,
            [CallerLineNumber] int lineNumber = 0);

        /// <summary>
        /// רישום שגיאה קריטית
        /// </summary>
        bool LogCriticalError(
            ErrorCode errorCode,
            string description,
            Exception? ex = null,
            string? reportName = null,
            int jobNumber = 0,
            [CallerMemberName] string? methodName = null,
            [CallerFilePath] string? filePath = null,
            [CallerLineNumber] int lineNumber = 0);
    }
}
