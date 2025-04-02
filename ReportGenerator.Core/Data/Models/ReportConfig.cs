namespace ReportGenerator.Core.Data.Models
{
    /// <summary>
    /// מודל המייצג הגדרת דוח במערכת
    /// </summary>
    public class ReportConfig
    {

        /// מזהה הדוח

        public int ReportID { get; set; }


        /// שם הדוח (גם שם התבנית)

        public string ReportName { get; set; }


        /// שמות הפרוצדורות לשליפת נתונים (מופרדות ב-;)

        public string StoredProcName { get; set; }


        /// כותרת הדוח

        public string Title { get; set; }


        /// תיאור הדוח (אופציונלי)

        public string Description { get; set; }
    }
}
