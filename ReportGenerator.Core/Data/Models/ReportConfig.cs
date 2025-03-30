namespace ReportGenerator.Core.Data.Models
{
    /// <summary>
    /// מודל המייצג הגדרת דוח במערכת
    /// </summary>
    public class ReportConfig
    {
        /// <summary>
        /// מזהה הדוח
        /// </summary>
        public int ReportID { get; set; }

        /// <summary>
        /// שם הדוח (גם שם התבנית)
        /// </summary>
        public string ReportName { get; set; }

        /// <summary>
        /// שמות הפרוצדורות לשליפת נתונים (מופרדות ב-;)
        /// </summary>
        public string StoredProcName { get; set; }

        /// <summary>
        /// כותרת הדוח
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// תיאור הדוח (אופציונלי)
        /// </summary>
        public string Description { get; set; }
    }
}
