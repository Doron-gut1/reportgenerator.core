using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using Microsoft.Extensions.Logging;
using ReportGenerator.Core.Interfaces;

namespace ReportGenerator.Core.Errors
{
    /// <summary>
    /// מנהל שגיאות מרכזי
    /// </summary>
    public class ErrorManager : IErrorManager
    {
        // רף החומרה לרישום שגיאות לDB
        public ErrorSeverity LogThreshold { get; set; } = ErrorSeverity.Warning;
        
        // רף החומרה להפסקת תהליך
        public ErrorSeverity BreakThreshold { get; set; } = ErrorSeverity.Critical;
        
        // מספר השגיאות שנרשמו
        private int _errorCount = 0;
        public int ErrorCount => _errorCount;
        
        // שגיאות אחרונות לכל סוג חומרה
        private Dictionary<ErrorSeverity, ErrorContext> _lastErrorsByType = new Dictionary<ErrorSeverity, ErrorContext>();
        
        // מונע כפילויות שגיאה באותה הפקת דוח
        private HashSet<string> _loggedErrorCodes = new HashSet<string>();
        
        // חסימה בשילוב מרובה חוטים
        private readonly object _lockObject = new object();
        
        // רישום השגיאה האחרונה
        public ErrorContext LastError { get; private set; }

        // לוגר לרישום שגיאות
        private readonly ILogger<ErrorManager> _logger;
        
        // רושם שגיאות
        private readonly IErrorLogger _errorLogger;

        /// <summary>
        /// יוצר מופע חדש של מנהל השגיאות
        /// </summary>
        /// <param name="errorLogger">רושם שגיאות</param>
        /// <param name="logger">לוגר</param>
        public ErrorManager(IErrorLogger errorLogger, ILogger<ErrorManager>? logger = null)
        {
            _errorLogger = errorLogger ?? throw new ArgumentNullException(nameof(errorLogger));
            _logger = logger;
        }
        
        /// <summary>
        /// מאפס את כל הנתונים של מערכת השגיאות
        /// </summary>
        public void Reset()
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
        public ErrorContext? GetLastErrorByType(ErrorSeverity severity)
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
        public bool HandleError(ErrorContext error)
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
                    
                    // רישום לטבלת שגיאות
                    _errorLogger.LogError(error);
                    
                    // סימון שנרשמה
                    error.IsLogged = true;
                }
                
                // רישום ללוגר אם קיים
                LogToLogger(error);
                
                // החלטה אם להמשיך את התהליך
                bool canContinue = error.Severity < BreakThreshold;
                
                return canContinue;
            }
        }
        
        /// <summary>
        /// רישום שגיאה ללוגר
        /// </summary>
        private void LogToLogger(ErrorContext error)
        {
            if (_logger == null)
                return;
                
            var logLevel = GetLogLevel(error.Severity);
            var exception = error.OriginalException;
            
            if (exception != null)
            {
                _logger.Log(logLevel, exception, "[{ErrorCode}] {ErrorDescription}", 
                    error.ErrorCode.ToString(), error.Description);
            }
            else
            {
                _logger.Log(logLevel, "[{ErrorCode}] {ErrorDescription}", 
                    error.ErrorCode.ToString(), error.Description);
            }
        }
        
        /// <summary>
        /// המרת רמת חומרת שגיאה לרמת לוג
        /// </summary>
        private LogLevel GetLogLevel(ErrorSeverity severity) => severity switch
        {
            ErrorSeverity.Information => LogLevel.Information,
            ErrorSeverity.Warning => LogLevel.Warning,
            ErrorSeverity.Error => LogLevel.Error,
            ErrorSeverity.Critical => LogLevel.Critical,
            _ => LogLevel.Information
        };
        
        /// <summary>
        /// יצירת חתימה ייחודית לשגיאה כדי למנוע כפילויות
        /// </summary>
        private string CreateErrorSignature(ErrorContext error)
        {
            // בהתבסס על הדוח, הקוד והמודול - מאפשר לזהות כפילויות שגיאה ספציפיות
            string reportPart = !string.IsNullOrEmpty(error.ReportName) ? error.ReportName : "NoReport";
            string jobPart = error.JobNumber > 0 ? error.JobNumber.ToString() : "NoJob";
            
            return $"{reportPart}_{jobPart}_{error.ErrorCode}_{error.ModuleName}_{error.MethodName}";
        }
        
        /// <summary>
        /// ניקוי רשימת השגיאות (בין הפקות דוחות)
        /// </summary>
        public void ClearErrors()
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
        public bool LogError(
            ErrorCode errorCode,
            ErrorSeverity severity,
            string description,
            Exception? ex = null,
            string? reportName = null,
            int jobNumber = 0,
            [CallerMemberName] string? methodName = null,
            [CallerFilePath] string? filePath = null,
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
        public bool LogInfo(
            ErrorCode errorCode,
            string description,
            Exception? ex = null,
            string? reportName = null,
            int jobNumber = 0,
            [CallerMemberName] string? methodName = null,
            [CallerFilePath] string? filePath = null,
            [CallerLineNumber] int lineNumber = 0)
        {
            return LogError(errorCode, ErrorSeverity.Information, description, ex, reportName, jobNumber, methodName, filePath, lineNumber);
        }
        
        /// <summary>
        /// רישום שגיאה מסוג אזהרה
        /// </summary>
        public bool LogWarning(
            ErrorCode errorCode,
            string description,
            Exception? ex = null,
            string? reportName = null,
            int jobNumber = 0,
            [CallerMemberName] string? methodName = null,
            [CallerFilePath] string? filePath = null,
            [CallerLineNumber] int lineNumber = 0)
        {
            return LogError(errorCode, ErrorSeverity.Warning, description, ex, reportName, jobNumber, methodName, filePath, lineNumber);
        }

        /// <summary>
        /// רישום שגיאה רגילה
        /// </summary>
        public bool LogNormalError(
            ErrorCode errorCode,
            string description,
            Exception? ex = null,
            string? reportName = null,
            int jobNumber = 0,
            [CallerMemberName] string? methodName = null,
            [CallerFilePath] string? filePath = null,
            [CallerLineNumber] int lineNumber = 0)
        {
            return LogError(errorCode, ErrorSeverity.Error, description, ex, reportName, jobNumber, methodName, filePath, lineNumber);
        }

        /// <summary>
        /// רישום שגיאה קריטית
        /// </summary>
        public bool LogCriticalError(
            ErrorCode errorCode,
            string description,
            Exception? ex = null,
            string? reportName = null,
            int jobNumber = 0,
            [CallerMemberName] string? methodName = null,
            [CallerFilePath] string? filePath = null,
            [CallerLineNumber] int lineNumber = 0)
        {
            return LogError(errorCode, ErrorSeverity.Critical, description, ex, reportName, jobNumber, methodName, filePath, lineNumber);
        }
    }
}
