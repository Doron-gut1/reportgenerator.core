using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using ReportGenerator.Core.Configuration;
using ReportGenerator.Core.Interfaces;
using System.IO;

namespace ReportGenerator.Core.Errors
{
    /// <summary>
    /// רושם שגיאות לבסיס נתונים
    /// </summary>
    public class DbErrorLogger : IErrorLogger
    {
        private static string _connectionString;
        private static string _logsFolder;
        private static bool _initialized = false;
        private static readonly object _lockObject = new object();

        /// <summary>
        /// יוצר מופע חדש של רושם שגיאות לבסיס נתונים
        /// </summary>
        public DbErrorLogger()
        {
        }

        /// <summary>
        /// יוצר מופע חדש של רושם שגיאות לבסיס נתונים עם הגדרות
        /// </summary>
        /// <param name="settings">הגדרות</param>
        public DbErrorLogger(IOptions<ReportSettings> settings)
        {
            var settingsValue = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
            Initialize(settingsValue.ConnectionString, settingsValue.LogsFolder);
        }

        /// <summary>
        /// אתחול מערכת רישום השגיאות
        /// </summary>
        /// <param name="connectionString">מחרוזת התחברות לבסיס הנתונים</param>
        /// <param name="logsFolder">תיקיית לוגים (אופציונלי)</param>
        public void Initialize(string connectionString, string logsFolder = null)
        {
            if (string.IsNullOrEmpty(connectionString))
                throw new ArgumentNullException(nameof(connectionString));

            lock (_lockObject)
            {
                _connectionString = connectionString;
                _logsFolder = logsFolder;
                _initialized = true;
            }
        }

        /// <summary>
        /// רושם שגיאה למערכת
        /// </summary>
        /// <param name="error">אובייקט שגיאה</param>
        public void LogError(ErrorContext error)
        {
            if (error == null)
                throw new ArgumentNullException(nameof(error));

            // וידוא שהמערכת אותחלה
            if (!_initialized)
            {
                throw new InvalidOperationException("Error logger has not been initialized. Call Initialize first.");
            }

            try
            {
                // ניסיון רישום למסד הנתונים
                LogToDatabase(error);
            }
            catch (Exception)
            {
                // אם נכשל, ננסה לרשום לקובץ (אם יש תיקייה)
                if (!string.IsNullOrEmpty(_logsFolder))
                {
                    LogToFile(error);
                }
            }
        }

        /// <summary>
        /// רישום שגיאה לבסיס הנתונים
        /// </summary>
        private static void LogToDatabase(ErrorContext error)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                
                // קריאה לפרוצדורה AddDbErrors הקיימת
                connection.Execute(
                    "AddDbErrors",
                    new
                    {
                        user = error.User,
                        errnum = error.ErrorCode.ToString(),  // המרה של enum למחרוזת
                        errdesc = error.Description,
                        modulname = error.ModuleName,
                        objectname = error.ReportName ?? string.Empty,
                        errline = error.LineNumber,
                        strinrow = error.Severity.ToString(),
                        moduletype = (int)error.Severity,
                        moredtls = error.AdditionalDetails ?? string.Empty,
                        comp = Environment.MachineName,
                        Ver = GetAssemblyVersion(),
                        CallStack = error.OriginalException?.StackTrace ?? string.Empty,
                        jobnum = error.JobNumber,
                        subname = error.MethodName
                    },
                    commandType: System.Data.CommandType.StoredProcedure);
            }
            catch (Exception)
            {
                // ייתכן שיש בעיה עם מסד הנתונים - נמשיך לרישום לקובץ
                throw;
            }
        }

        /// <summary>
        /// רישום שגיאה לקובץ
        /// </summary>
        private static void LogToFile(ErrorContext error)
        {
            try
            {
                // וידוא שהתיקייה קיימת
                Directory.CreateDirectory(_logsFolder);
                
                // יצירת שם קובץ ייחודי ליום
                string fileName = Path.Combine(_logsFolder, $"ErrorLog_{DateTime.Now:yyyyMMdd}.log");
                
                // הכנת מחרוזת השגיאה
                string errorText = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} | " +
                                   $"[{error.Severity}] | " +
                                   $"{error.ErrorCode} | " +
                                   $"{error.Description} | " +
                                   $"Method: {error.MethodName} | " +
                                   $"Module: {error.ModuleName}:{error.LineNumber} | " +
                                   $"Report: {error.ReportName ?? "N/A"}" +
                                   Environment.NewLine;
                
                if (error.OriginalException != null)
                {
                    errorText += $"Exception: {error.OriginalException}" + Environment.NewLine;
                    errorText += $"StackTrace: {error.OriginalException.StackTrace}" + Environment.NewLine;
                }
                
                // הוספת שורת הפרדה
                errorText += "-----------------------------------------------------" + Environment.NewLine;
                
                // רישום לקובץ
                File.AppendAllText(fileName, errorText);
            }
            catch
            {
                // כשל גם ברישום לקובץ - לא ניתן לעשות יותר
            }
        }

        /// <summary>
        /// קבלת גרסת האסמבלי הנוכחי
        /// </summary>
        private static string GetAssemblyVersion()
        {
            return typeof(DbErrorLogger).Assembly.GetName().Version?.ToString() ?? "1.0.0.0";
        }
    }
}
