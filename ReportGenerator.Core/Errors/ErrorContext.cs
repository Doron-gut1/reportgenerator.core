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
        /// מזהה ייחודי לסוג השגיאה
        public string ErrorCode { get; set; }

        /// רמת חומרת השגיאה
        public ErrorSeverity Severity { get; set; }

        /// תיאור השגיאה

        public string Description { get; set; }

        /// מידע נוסף על השגיאה
        public string AdditionalDetails { get; set; }

        /// שם המשתמש
        public string User { get; set; }

        /// שם המודול בו התרחשה השגיאה
        public string ModuleName { get; set; }

        /// שם המתודה בה התרחשה השגיאה
        public string MethodName { get; set; }

        /// שם הקובץ בו התרחשה השגיא
        public string FileName { get; set; }

        /// מספר השורה בה התרחשה השגיאה
        public int LineNumber { get; set; }

        /// החריגה המקורית
        public Exception OriginalException { get; set; }

        /// שם הדוח שהופק (אם רלוונטי)
        public string ReportName { get; set; }

        /// מספר הפקת הדוח (אם רלוונטי)
        public int JobNumber { get; set; }

        /// זמן התרחשות השגיאה
        public DateTime Timestamp { get; set; }

        /// שם המחשב
        public string MachineName { get; set; }

        /// גרסת האפליקציה
        public string AppVersion { get; set; }

        /// האם השגיאה נרשמה ל-DB
        public bool IsLogged { get; set; }

        /// בנאי חדש לאובייקט ErrorContext

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

        /// יצירת מחרוזת המתארת את השגיאה
        public override string ToString()
        {
            return $"[{Severity}] {ErrorCode}: {Description} ({MethodName} in {FileName}:{LineNumber})";
        }
    }
}