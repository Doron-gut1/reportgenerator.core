namespace ReportGenerator.Core.Configuration
{
    /// <summary>
    /// הגדרות מרכזיות למערכת הדוחות
    /// </summary>
    public class 
        ReportSettings
    {
        /// <summary>
        /// נתיב לתיקיית תבניות HTML
        /// </summary>
        public string TemplatesFolder { get; set; } = string.Empty;

        /// <summary>
        /// נתיב לתיקיית פלט של דוחות
        /// </summary>
        public string OutputFolder { get; set; } = string.Empty;

        /// <summary>
        /// נתיב לתיקיית מידע זמני
        /// </summary>
        //public string TempFolder { get; set; } = "Temp";

        /// <summary>
        /// נתיב לתיקיית לוגים
        /// </summary>
        public string LogsFolder { get; set; } = string.Empty;

        /// <summary>
        /// נתיב להפעלת כרום (אופציונלי)
        /// </summary>
        public string ChromePath { get; set; } = string.Empty;

        /// <summary>
        /// מחרוזת התחברות לבסיס הנתונים
        /// </summary>
        public string ConnectionString { get; set; } = string.Empty;

        /// <summary>
        /// האם להשתמש בהורדה אוטומטית של כרום
        /// </summary>
        public bool AutoDownloadChrome { get; set; } = true;
    }
}
