namespace ReportGenerator.Api.Models
{
    /// <summary>
    /// בקשה להפקת דוח
    /// </summary>
    public class GenerateReportRequest
    {
        /// <summary>
        /// שם הדוח לפי הגדרתו בבסיס הנתונים
        /// </summary>
        public required string ReportName { get; set; }

        /// <summary>
        /// מזהה המחלקה (תיקייה בתוך תיקיית התבניות)
        /// </summary>
        public string? DepartmentId { get; set; }

        /// <summary>
        /// פורמט הפלט (PDF או Excel)
        /// </summary>
        public required string OutputFormat { get; set; }

        /// <summary>
        /// פרמטרים להפקת הדוח
        /// </summary>
        public Dictionary<string, ReportParameterModel>? Parameters { get; set; }
    }

    /// <summary>
    /// פרמטר לדוח
    /// </summary>
    public class ReportParameterModel
    {
        /// <summary>
        /// ערך הפרמטר
        /// </summary>
        public required object Value { get; set; }

        /// <summary>
        /// סוג הנתונים (Int32, String, Date, וכו')
        /// </summary>
        public required string Type { get; set; }
    }
}
