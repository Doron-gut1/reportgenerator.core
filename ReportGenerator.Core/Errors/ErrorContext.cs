using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

namespace ReportGenerator.Core.Errors
{
    /// <summary>
    /// מחלקה המייצגת את הקשר השגיאה
    /// </summary>
    public class ErrorContext
    {
        #region Properties
        
        /// <summary>
        /// מזהה ייחודי לסוג השגיאה
        /// </summary>
        public string ErrorCode { get; set; }

        /// <summary>
        /// רמת חומרת השגיאה
        /// </summary>
        public ErrorSeverity Severity { get; set; }

        /// <summary>
        /// תיאור השגיאה
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// מידע נוסף על השגיאה
        /// </summary>
        public string AdditionalDetails { get; set; }

        /// <summary>
        /// שם המשתמש
        /// </summary>
        public string User { get; set; }

        /// <summary>
        /// שם המודול בו התרחשה השגיאה
        /// </summary>
        public string ModuleName { get; set; }

        /// <summary>
        /// שם המתודה בה התרחשה השגיאה
        /// </summary>
        public string MethodName { get; set; }

        /// <summary>
        /// שם הקובץ בו התרחשה השגיא
        /// </summary>
        public string FileName { get; set; }

        /// <summary>
        /// מספר השורה בה התרחשה השגיאה
        /// </summary>
        public int LineNumber { get; set; }

        /// <summary>
        /// החריגה המקורית
        /// </summary>
        public Exception OriginalException { get; set; }

        /// <summary>
        /// שם הדוח שהופק (אם רלוונטי)
        /// </summary>
        public string ReportName { get; set; }

        /// <summary>
        /// מספר הפקת הדוח (אם רלוונטי)
        /// </summary>
        public int JobNumber { get; set; }

        /// <summary>
        /// זמן התרחשות השגיאה
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// שם המחשב
        /// </summary>
        public string MachineName { get; set; }

        /// <summary>
        /// גרסת האפליקציה
        /// </summary>
        public string AppVersion { get; set; }

        /// <summary>
        /// האם השגיאה נרשמה ל-DB
        /// </summary>
        public bool IsLogged { get; set; }
        
        /// <summary>
        /// זמן מאז התרחשות השגיאה
        /// </summary>
        public TimeSpan TimeSinceOccurred => DateTime.Now - Timestamp;
        
        #endregion

        #region Constructors
        
        /// <summary>
        /// בנאי חדש לאובייקט ErrorContext
        /// </summary>
        public ErrorContext(
            string errorCode,
            ErrorSeverity severity,
            string description,
            [CallerMemberName] string methodName = null,
            [CallerFilePath] string filePath = null,
            [CallerLineNumber] int lineNumber = 0)
        {
            if (string.IsNullOrEmpty(errorCode))
                throw new ArgumentException("Error code cannot be null or empty", nameof(errorCode));
                
            ErrorCode = errorCode;
            Severity = severity;
            Description = description ?? "No description provided";
            MethodName = methodName;
            FileName = filePath != null ? Path.GetFileName(filePath) : null;
            LineNumber = lineNumber;
            ModuleName = filePath != null ? Path.GetFileNameWithoutExtension(filePath) : null;
            Timestamp = DateTime.Now;
            MachineName = Environment.MachineName;
            User = Environment.UserName;
            AppVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
            IsLogged = false;
        }
        
        /// <summary>
        /// יצירת אובייקט שגיאה מחריגה
        /// </summary>
        public static ErrorContext FromException(
            Exception exception,
            string errorCode,
            ErrorSeverity severity = ErrorSeverity.Error,
            [CallerMemberName] string methodName = null,
            [CallerFilePath] string filePath = null,
            [CallerLineNumber] int lineNumber = 0)
        {
            if (exception == null)
                throw new ArgumentNullException(nameof(exception));
                
            return new ErrorContext(
                errorCode,
                severity, 
                exception.Message,
                methodName,
                filePath,
                lineNumber)
            {
                OriginalException = exception,
                AdditionalDetails = exception.ToString()
            };
        }
        
        #endregion

        #region Methods
        
        /// <summary>
        /// יצירת מחרוזת המתארת את השגיאה
        /// </summary>
        public override string ToString()
        {
            return $"[{Severity}] {ErrorCode}: {Description} ({MethodName} in {FileName}:{LineNumber})";
        }
        
        /// <summary>
        /// יצירת מחרוזת מפורטת של השגיאה
        /// </summary>
        public string ToDetailedString()
        {
            var sb = new StringBuilder();
            
            sb.AppendLine($"===== Error Details: {ErrorCode} =====");
            sb.AppendLine($"Severity: {Severity}");
            sb.AppendLine($"Description: {Description}");
            sb.AppendLine($"Time: {Timestamp:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Location: {MethodName} in {FileName}:{LineNumber}");
            
            if (!string.IsNullOrEmpty(ReportName))
                sb.AppendLine($"Report: {ReportName}");
                
            if (JobNumber > 0)
                sb.AppendLine($"Job: {JobNumber}");
                
            sb.AppendLine($"User: {User}");
            sb.AppendLine($"Machine: {MachineName}");
            
            if (OriginalException != null)
            {
                sb.AppendLine();
                sb.AppendLine("Original Exception:");
                sb.AppendLine(OriginalException.ToString());
            }
            
            return sb.ToString();
        }
        
        /// <summary>
        /// יצירת הודעת שגיאה ידידותית למשתמש
        /// </summary>
        public string ToUserFriendlyMessage()
        {
            string severityText = Severity switch
            {
                ErrorSeverity.Information => "מידע",
                ErrorSeverity.Warning => "אזהרה",
                ErrorSeverity.Error => "שגיאה",
                ErrorSeverity.Critical => "שגיאה קריטית",
                _ => "שגיאה"
            };
            
            var sb = new StringBuilder();
            sb.AppendLine($"{severityText}: {Description}");
            
            // הוספת מידע רלוונטי (רק עבור שגיאות לא קריטיות)
            if (Severity <= ErrorSeverity.Error)
            {
                if (!string.IsNullOrEmpty(ReportName))
                    sb.AppendLine($"דוח: {ReportName}");
            }
            
            // הוספת מזהה שגיאה לצורך דיווח
            sb.AppendLine($"מזהה שגיאה: {ErrorCode}");
            
            return sb.ToString();
        }
        
        #endregion
    }
}
