using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;

namespace ReportGenerator.Core.Errors
{
    /// <summary>
    /// מנהל שגיאות מרכזי
    /// </summary>
    public static class ErrorManager
    {
        // רף החומרה לרישום שגיאות ל-DB
        public static ErrorSeverity LogThreshold { get; set; } = ErrorSeverity.Warning;
        
        // רף החומרה להפסקת תהליך
        public static ErrorSeverity BreakThreshold { get; set; } = ErrorSeverity.Critical;
        
        // מונע כפילויות שגיאות באותה הפקת דוח
        private static HashSet<string> _loggedErrorCodes = new HashSet<string>();
        
        // תיעוד השגיאה האחרונה
        public static ErrorContext LastError { get; private set; }
        

        /// טיפול בשגיאה וקבלת החלטה אם להמשיך

        /// <param name="error">אובייקט שגיאה</param>
        /// <returns>האם ניתן להמשיך בתהליך</returns>
        public static bool HandleError(ErrorContext error)
        {
            // שמירת השגיאה האחרונה
            LastError = error;
            
            // בדיקה אם צריך לרשום ל-DB
            bool shouldLog = error.Severity >= LogThreshold;
            
            // בדיקה אם כבר רשמנו שגיאה מאותו סוג
            string errorSignature = $"{error.ReportName ?? ""}_{error.ErrorCode}";
            bool isDuplicate = _loggedErrorCodes.Contains(errorSignature);
            
            // רישום שגיאה ל-DB אם צריך
            if (shouldLog && !isDuplicate)
            {
                DbErrorLogger.LogError(error);
                _loggedErrorCodes.Add(errorSignature);
                error.IsLogged = true;
            }           
            // החלטה אם להמשיך את התהליך
            bool canContinue = error.Severity < BreakThreshold;
            
            return canContinue;
        }
        
        /// ניקוי רשימת השגיאות (בין הפקות דוחות)
        public static void ClearErrors()
        {
            _loggedErrorCodes.Clear();
            LastError = null;
        }
        

        /// יצירת שגיאה ושליחתה לטיפול

        /// <param name="errorCode">קוד שגיאה</param>
        /// <param name="severity">רמת חומרה</param>
        /// <param name="description">תיאור השגיאה</param>
        /// <param name="ex">החריגה המקורית (אופציונלי)</param>
        /// <param name="reportName">שם הדוח (אופציונלי)</param>
        /// <param name="methodName">שם המתודה (אוטומטי)</param>
        /// <param name="filePath">נתיב הקובץ (אוטומטי)</param>
        /// <param name="lineNumber">מספר השורה (אוטומטי)</param>
        /// <returns>האם ניתן להמשיך בתהליך</returns>
        public static bool LogError(
            string errorCode,
            ErrorSeverity severity,
            string description,
            Exception ex = null,
            string reportName = null,
            [CallerMemberName] string methodName = null,
            [CallerFilePath] string filePath = null,
            [CallerLineNumber] int lineNumber = 0)
        {
            var error = new ErrorContext(errorCode, severity, description, methodName, filePath, lineNumber)
            {
                OriginalException = ex,
                ReportName = reportName,
                AdditionalDetails = ex?.ToString()
            };
            
            return HandleError(error);
        }
        
        /// רישום שגיאה מסוג מידע
        public static bool LogInfo(
            string errorCode,
            string description,
            Exception ex = null,
            string reportName = null,
            [CallerMemberName] string methodName = null,
            [CallerFilePath] string filePath = null,
            [CallerLineNumber] int lineNumber = 0)
        {
            return LogError(errorCode, ErrorSeverity.Information, description, ex, reportName, methodName, filePath, lineNumber);
        }
        /// רישום שגיאה מסוג אזהרה

        public static bool LogWarning(
            string errorCode,
            string description,
            Exception ex = null,
            string reportName = null,
            [CallerMemberName] string methodName = null,
            [CallerFilePath] string filePath = null,
            [CallerLineNumber] int lineNumber = 0)
        {
            return LogError(errorCode, ErrorSeverity.Warning, description, ex, reportName, methodName, filePath, lineNumber);
        }


        /// רישום שגיאה רגילה

        public static bool LogNormalError(
            string errorCode,
            string description,
            Exception ex = null,
            string reportName = null,
            [CallerMemberName] string methodName = null,
            [CallerFilePath] string filePath = null,
            [CallerLineNumber] int lineNumber = 0)
        {
            return LogError(errorCode, ErrorSeverity.Error, description, ex, reportName, methodName, filePath, lineNumber);
        }


        /// רישום שגיאה קריטית

        public static bool LogCriticalError(
            string errorCode,
            string description,
            Exception ex = null,
            string reportName = null,
            [CallerMemberName] string methodName = null,
            [CallerFilePath] string filePath = null,
            [CallerLineNumber] int lineNumber = 0)
        {
            return LogError(errorCode, ErrorSeverity.Critical, description, ex, reportName, methodName, filePath, lineNumber);
        }
    }
}