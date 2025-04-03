// הוסף קובץ חדש: Errors/ReportNotFoundException.cs
using System;

namespace ReportGenerator.Core.Errors
{
    /// <summary>
    /// חריגה ייעודית למקרה שדוח לא נמצא
    /// </summary>
    public class ReportNotFoundException : Exception
    {
        public string ReportName { get; }

        public ReportNotFoundException(string message) : base(message)
        {
        }

        public ReportNotFoundException(string message, string reportName) : base(message)
        {
            ReportName = reportName;
        }

        public ReportNotFoundException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}