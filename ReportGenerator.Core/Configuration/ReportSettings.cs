namespace ReportGenerator.Core.Configuration
{
    /// <summary>
    /// הגדרות מרכזיות למערכת הדוחות
    /// </summary>
    public class ReportSettings
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
        /// נתיב לתיקיית לוגים
        /// </summary>
       // public string LogsFolder { get; set; } = "c:\temp";

        /// <summary>
        /// נתיב להפעלת כרום (אופציונלי)
        /// </summary>
        public string ChromePath { get; set; } = @"C:\Program Files\Google\Chrome\Application";

        /// <summary>
        /// מחרוזת התחברות לבסיס הנתונים
        /// </summary>
        public string ConnectionString { get; set; } = string.Empty;

        /// <summary>
        /// האם להשתמש בהורדה אוטומטית של כרום
        /// </summary>
        public bool AutoDownloadChrome { get; set; } = false;
    }
}
