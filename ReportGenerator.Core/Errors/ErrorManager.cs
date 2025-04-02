using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;

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
        
        // מספר השגיאות שנרשמו
        private static int _errorCount = 0;
        public static int ErrorCount => _errorCount;
        
        // שגיאות אחרונות לכל סוג חומרה
        private static Dictionary<ErrorSeverity, ErrorContext> _lastErrorsByType = new Dictionary<ErrorSeverity, ErrorContext>();
        
        // מונע כפילויות שגיאות באותה הפקת דוח
        private static HashSet<string> _loggedErrorCodes = new HashSet<string>();
        
        // חסימה בשילוב מרובה חוטים
        private static readonly object _lockObject = new object();
        
        // תיעוד השגיאה האחרונה
        public static ErrorContext LastError { get; private set; }
        
        /// <summary>
        /// מאפס את כל הנתונים של מערכת השגיאות
        /// </summary>
        public static void Reset()
        {
            lock (_lockObject)
            {
                _loggedErrorCodes.Clear();
                _lastErrorsByType.Clear();
                LastError = null;
                _errorCount = 0;
            }
        }
        
        /// <summary>
        /// קבלת השגיאה האחרונה מסוג חומרה ספציפי
        /// </summary>
        /// <param name="severity">סוג החומרה</param>
        /// <returns>אובייקט השגיאה או null</returns>
        public static ErrorContext GetLastErrorByType(ErrorSeverity severity)
        {
            lock (_lockObject)
            {
                _lastErrorsByType.TryGetValue(severity, out ErrorContext error);
                return error;
            }
        }

        /// <summary>
        /// טיפול בשגיאה וקבלת החלטה אם להמשיך
        /// </summary>
        /// <param name="error">אובייקט שגיאה</param>
        /// <returns>האם ניתן להמשיך בתהליך</returns>
        public static bool HandleError(ErrorContext error)
        {
            if (error == null)
                throw new ArgumentNullException(nameof(error));
                
            lock (_lockObject)
            {
                // שמירת השגיאה האחרונה ועדכון סטטיסטיקות
                LastError = error;
                _lastErrorsByType[error.Severity] = error;
                Interlocked.Increment(ref _errorCount);
                
                // בדיקה אם צריך לרשום ל-DB
                bool shouldLog = error.Severity >= LogThreshold;
                
                // יצירת מזהה ייחודי לשגיאה בהקשר של הפקת דוח ספציפית
                string errorSignature = CreateErrorSignature(error);
                bool isDuplicate = _loggedErrorCodes.Contains(errorSignature);
                
                // רישום שגיאה ל-DB אם צריך
                if (shouldLog && !isDuplicate)
                {
                    // הוספה לרשימת השגיאות שכבר נרשמו
                    _loggedErrorCodes.Add(errorSignature);
                    
                    // רישום בטבלת שגיאות
                    DbErrorLogger.LogError(error);
                    
                    // סימון שנרשמה
                    error.IsLogged = true;
                }
                
                // רישום לקונסולה במקרה של שגיאה חמורה
                if (error.Severity >= ErrorSeverity.Error)
                {
                    Console.WriteLine($"שגיאה [{error.Severity}]: {error.ErrorCode} - {error.Description}");
                }
                
                // החלטה אם להמשיך את התהליך
                bool canContinue = error.Severity < BreakThreshold;
                
                return canContinue;
            }
        }
        
        /// <summary>
        /// יצירת חתימה ייחודית לשגיאה כדי למנוע כפילויות
        /// </summary>
        private static string CreateErrorSignature(ErrorContext error)
        {
            // בהתבסס על הדוח, הקוד והמודול - מאפשר לזהות כפילויות שגיאה ספציפיות
            string reportPart = !string.IsNullOrEmpty(error.ReportName) ? error.ReportName : "NoReport";
            string jobPart = error.JobNumber > 0 ? error.JobNumber.ToString() : "NoJob";
            
            return $"{reportPart}_{jobPart}_{error.ErrorCode}_{error.ModuleName}_{error.MethodName}";
        }
        
        /// <summary>
        /// ניקוי רשימת השגיאות (בין הפקות דוחות)
        /// </summary>
        public static void ClearErrors()
        {
            lock (_lockObject)
            {
                _loggedErrorCodes.Clear();
                LastError = null;
            }
        }

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
        public static bool LogError(
            string errorCode,
            ErrorSeverity severity,
            string description,
            Exception ex = null,
            string reportName = null,
            int jobNumber = 0,
            [CallerMemberName] string methodName = null,
            [CallerFilePath] string filePath = null,
            [CallerLineNumber] int lineNumber = 0)
        {
            var error = new ErrorContext(errorCode, severity, description, methodName, filePath, lineNumber)
            {
                OriginalException = ex,
                ReportName = reportName,
                JobNumber = jobNumber,
                AdditionalDetails = ex?.ToString()
            };
            
            return HandleError(error);
        }
        
        /// <summary>
        /// רישום שגיאה מסוג מידע
        /// </summary>
        public static bool LogInfo(
            string errorCode,
            string description,
            Exception ex = null,
            string reportName = null,
            int jobNumber = 0,
            [CallerMemberName] string methodName = null,
            [CallerFilePath] string filePath = null,
            [CallerLineNumber] int lineNumber = 0)
        {
            return LogError(errorCode, ErrorSeverity.Information, description, ex, reportName, jobNumber, methodName, filePath, lineNumber);
        }
        
        /// <summary>
        /// רישום שגיאה מסוג אזהרה
        /// </summary>
        public static bool LogWarning(
            string errorCode,
            string description,
            Exception ex = null,
            string reportName = null,
            int jobNumber = 0,
            [CallerMemberName] string methodName = null,
            [CallerFilePath] string filePath = null,
            [CallerLineNumber] int lineNumber = 0)
        {
            return LogError(errorCode, ErrorSeverity.Warning, description, ex, reportName, jobNumber, methodName, filePath, lineNumber);
        }

        /// <summary>
        /// רישום שגיאה רגילה
        /// </summary>
        public static bool LogNormalError(
            string errorCode,
            string description,
            Exception ex = null,
            string reportName = null,
            int jobNumber = 0,
            [CallerMemberName] string methodName = null,
            [CallerFilePath] string filePath = null,
            [CallerLineNumber] int lineNumber = 0)
        {
            return LogError(errorCode, ErrorSeverity.Error, description, ex, reportName, jobNumber, methodName, filePath, lineNumber);
        }

        /// <summary>
        /// רישום שגיאה קריטית
        /// </summary>
        public static bool LogCriticalError(
            string errorCode,
            string description,
            Exception ex = null,
            string reportName = null,
            int jobNumber = 0,
            [CallerMemberName] string methodName = null,
            [CallerFilePath] string filePath = null,
            [CallerLineNumber] int lineNumber = 0)
        {
            return LogError(errorCode, ErrorSeverity.Critical, description, ex, reportName, jobNumber, methodName, filePath, lineNumber);
        }
    }
}
