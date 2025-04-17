using System;
using System.IO;

namespace ReportGenerator.Core.Errors
{
    /// <summary>
    /// מידע מלא על שגיאה
    /// </summary>
    public class ErrorContext
    {
        /// <summary>
        /// קוד שגיאה
        /// </summary>
        public ErrorCode ErrorCode { get; }
        
        /// <summary>
        /// רמת חומרת השגיאה
        /// </summary>
        public ErrorSeverity Severity { get; }
        
        /// <summary>
        /// תיאור השגיאה
        /// </summary>
        public string Description { get; }
        
        /// <summary>
        /// זמן התרחשות השגיאה
        /// </summary>
        public DateTime Timestamp { get; } = DateTime.Now;
        
        /// <summary>
        /// שם המשתמש 
        /// </summary>
        public string User { get; set; } = "SYSTEM";
        
        /// <summary>
        /// שם המתודה שבה קרתה השגיאה
        /// </summary>
        public string MethodName { get; }
        
        /// <summary>
        /// שם הקובץ
        /// </summary>
        public string ModuleName { get; }
        
        /// <summary>
        /// מספר השורה
        /// </summary>
        public int LineNumber { get; }
        
        /// <summary>
        /// פרטים נוספים
        /// </summary>
        public string AdditionalDetails { get; set; } = string.Empty;
        
        /// <summary>
        /// החריגה המקורית
        /// </summary>
        public Exception? OriginalException { get; set; }
        
        /// <summary>
        /// שם הדוח שבהפקתו קרתה השגיאה
        /// </summary>
        public string? ReportName { get; set; }
        
        /// <summary>
        /// מספר הפקת דוח
        /// </summary>
        public int JobNumber { get; set; }
        
        /// <summary>
        /// האם נרשמה לDB
        /// </summary>
        public bool IsLogged { get; set; }
        
        /// <summary>
        /// יוצר מופע חדש של אובייקט שגיאה
        /// </summary>
        public ErrorContext(
            ErrorCode errorCode,
            ErrorSeverity severity,
            string description,
            string? methodName = null,
            string? filePath = null,
            int lineNumber = 0)
        {
            ErrorCode = errorCode;
            Severity = severity;
            Description = description ?? throw new ArgumentNullException(nameof(description));
            MethodName = methodName ?? "Unknown";
            ModuleName = filePath != null ? Path.GetFileName(filePath) : "Unknown";
            LineNumber = lineNumber;
        }

        /// <summary>
        /// מחרוזת ייצוג לשגיאה
        /// </summary>
        /// <returns>מחרוזת תיאור השגיאה</returns>
        public override string ToString()
        {
            return $"[{Severity}] {ErrorCode}: {Description} in {ModuleName}.{MethodName} line {LineNumber}";
        }
    }
}
