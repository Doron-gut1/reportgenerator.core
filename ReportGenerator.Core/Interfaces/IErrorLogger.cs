using ReportGenerator.Core.Errors;

namespace ReportGenerator.Core.Interfaces
{
    /// <summary>
    /// ממשק לרישום שגיאות
    /// </summary>
    public interface IErrorLogger
    {
        /// <summary>
        /// רושם שגיאה לבסיס נתונים או לקובץ לוג
        /// </summary>
        /// <param name="error">אובייקט השגיאה</param>
        void LogError(ErrorContext error);
        
        /// <summary>
        /// אתחול מערכת רישום השגיאות
        /// </summary>
        /// <param name="connectionString">מחרוזת התחברות לבסיס הנתונים</param>
        /// <param name="logsFolder">תיקיית לוגים (אופציונלי)</param>
        void Initialize(string connectionString, string logsFolder = null);
    }
}
