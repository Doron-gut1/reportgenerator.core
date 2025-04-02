using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace ReportGenerator.Core.Errors
{
    /// <summary>
    /// מחלקה המייצגת את הקשר השגיאה
    /// </summary>
    public class ErrorContext
    {
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
        /// שם הקובץ בו התרחשה השגיאה
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
            ErrorCode = errorCode;
            Severity = severity;
            Description = description;
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
        /// יצירת מחרוזת המתארת את השגיאה
        /// </summary>
        public override string ToString()
        {
            return $"[{Severity}] {ErrorCode}: {Description} ({MethodName} in {FileName}:{LineNumber})";
        }
    }
}