using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using Microsoft.Data.SqlClient;
using System.Reflection;
using System.Data;
using Dapper;

namespace ReportGenerator.Core.Errors
{
    /// <summary>
    /// מערכת רישום שגיאות פשוטה להחלפת מערכת השגיאות המורכבת
    /// תומכת ברישום שגיאות קריטיות מפורט לDB ושגיאות רגילות לקונסול/לוג מקומי
    /// </summary>
    public static class SimpleLogger
    {
        private static string _connectionString;
        private static string _logsFolder;
        private static bool _initialized = false;
        private static readonly object _lockObject = new object();

        /// <summary>
        /// אתחול מערכת הלוגים
        /// </summary>
        /// <param name="connectionString">מחרוזת חיבור לבסיס הנתונים</param>
        /// <param name="logsFolder">תיקיית לוגים מקומיים (אופציונלי)</param>
        public static void Initialize(string connectionString, string logsFolder = null)
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
        /// רישום הודעת מידע
        /// </summary>
        /// <param name="message">הודעה</param>
        /// <param name="reportName">שם הדוח (אופציונלי)</param>
        /// <param name="methodName">שם המתודה (אוטומטי)</param>
        public static void LogInfo(
            string message, 
            string reportName = null,
            [CallerMemberName] string methodName = null)
        {
            WriteToConsole("INFO", message, reportName, methodName);
        }

        /// <summary>
        /// רישום אזהרה
        /// </summary>
        /// <param name="message">הודעה</param>
        /// <param name="reportName">שם הדוח (אופציונלי)</param>
        /// <param name="methodName">שם המתודה (אוטומטי)</param>
        public static void LogWarning(
            string message, 
            string reportName = null,
            [CallerMemberName] string methodName = null)
        {
            WriteToConsole("WARNING", message, reportName, methodName);
        }

        /// <summary>
        /// רישום שגיאה רגילה - לא חוסמת תהליך
        /// </summary>
        /// <param name="message">הודעת שגיאה</param>
        /// <param name="ex">חריגה (אופציונלי)</param>
        /// <param name="reportName">שם הדוח (אופציונלי)</param>
        /// <param name="methodName">שם המתודה (אוטומטי)</param>
        /// <param name="filePath">נתיב הקובץ (אוטומטי)</param>
        /// <param name="lineNumber">מספר השורה (אוטומטי)</param>
        public static void LogError(
            string message, 
            Exception ex = null, 
            string reportName = null,
            [CallerMemberName] string methodName = null,
            [CallerFilePath] string filePath = null,
            [CallerLineNumber] int lineNumber = 0)
        {
            WriteToConsole("ERROR", message, reportName, methodName, ex);
            
            // רישום לקובץ מקומי אם הוגדר
            if (!string.IsNullOrEmpty(_logsFolder))
            {
                WriteToLocalFile("ERROR", message, ex, reportName, methodName, filePath, lineNumber);
            }
        }

        /// <summary>
        /// רישום שגיאה קריטית - חוסמת תהליך ונרשמת מלא לDB
        /// </summary>
        /// <param name="message">הודעת שגיאה</param>
        /// <param name="ex">חריגה (אופציונלי)</param>
        /// <param name="reportName">שם הדוח (אופציונלי)</param>
        /// <param name="parameters">פרמטרים נוספים לתיעוד (אופציונלי)</param>
        /// <param name="methodName">שם המתודה (אוטומטי)</param>
        /// <param name="filePath">נתיב הקובץ (אוטומטי)</param>
        /// <param name="lineNumber">מספר השורה (אוטומטי)</param>
        /// <returns>תמיד מחזיר false (התהליך לא יכול להמשיך)</returns>
        public static bool LogCriticalError(
            string message, 
            Exception ex = null, 
            string reportName = null,
            Dictionary<string, object> parameters = null,
            [CallerMemberName] string methodName = null,
            [CallerFilePath] string filePath = null,
            [CallerLineNumber] int lineNumber = 0)
        {
            // רישום לקונסול
            WriteToConsole("CRITICAL", message, reportName, methodName, ex);
            
            // רישום מפורט לDB
            try
            {
                LogToDatabase(message, ex, reportName, parameters, methodName, filePath, lineNumber);
            }
            catch (Exception dbEx)
            {
                // אם נכשל רישום לDB, לפחות נרשום לקובץ
                WriteToConsole("ERROR", $"נכשל רישום לDB: {dbEx.Message}", reportName, methodName);
                
                if (!string.IsNullOrEmpty(_logsFolder))
                {
                    WriteToLocalFile("CRITICAL", $"{message} | DB_LOG_FAILED: {dbEx.Message}", ex, reportName, methodName, filePath, lineNumber);
                }
            }

            // שגיאה קריטית תמיד מחזירה false (לא ניתן להמשיך)
            return false;
        }

        /// <summary>
        /// רישום לקונסול/דיבוג
        /// </summary>
        private static void WriteToConsole(string level, string message, string reportName, string methodName, Exception ex = null)
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            string reportInfo = !string.IsNullOrEmpty(reportName) ? $" | Report: {reportName}" : "";
            string methodInfo = !string.IsNullOrEmpty(methodName) ? $" | Method: {methodName}" : "";
            
            string logMessage = $"{timestamp} | [{level}] {message}{reportInfo}{methodInfo}";
            
            if (ex != null)
            {
                logMessage += $" | Exception: {ex.Message}";
            }
            
            Console.WriteLine(logMessage);
            System.Diagnostics.Debug.WriteLine(logMessage);
        }

        /// <summary>
        /// רישום לקובץ מקומי
        /// </summary>
        private static void WriteToLocalFile(string level, string message, Exception ex, string reportName, string methodName, string filePath, int lineNumber)
        {
            try
            {
                // וידוא שהתיקייה קיימת
                Directory.CreateDirectory(_logsFolder);
                
                // יצירת שם קובץ ייחודי ליום
                string fileName = Path.Combine(_logsFolder, $"ErrorLog_{DateTime.Now:yyyyMMdd}.log");
                
                // הכנת מחרוזת השגיאה המפורטת
                string errorText = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} | [{level}] {message}" + Environment.NewLine;
                errorText += $"  Report: {reportName ?? "N/A"}" + Environment.NewLine;
                errorText += $"  Method: {methodName ?? "N/A"}" + Environment.NewLine;
                errorText += $"  File: {Path.GetFileName(filePath ?? "N/A")}:{lineNumber}" + Environment.NewLine;
                
                if (ex != null)
                {
                    errorText += $"  Exception: {ex.GetType().Name}: {ex.Message}" + Environment.NewLine;
                    errorText += $"  StackTrace: {ex.StackTrace}" + Environment.NewLine;
                    
                    // InnerException אם קיים
                    if (ex.InnerException != null)
                    {
                        errorText += $"  InnerException: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}" + Environment.NewLine;
                    }
                }
                
                errorText += "-----------------------------------------------------" + Environment.NewLine;
                
                // רישום לקובץ
                File.AppendAllText(fileName, errorText);
            }
            catch
            {
                // כשל ברישום לקובץ - אין מה לעשות
            }
        }

        /// <summary>
        /// רישום מפורט לבסיס הנתונים (רק לשגיאות קריטיות)
        /// </summary>
        private static void LogToDatabase(string message, Exception ex, string reportName, Dictionary<string, object> parameters, string methodName, string filePath, int lineNumber)
        {
            if (!_initialized)
            {
                throw new InvalidOperationException("SimpleLogger לא אותחל. יש לקרוא ל-Initialize תחילה.");
            }

            try
            {
                using var connection = new SqlConnection(_connectionString);
                
                // הכנת פרטים נוספים
                string moreDetails = PrepareMoreDetails(parameters, ex);
                string fileName = !string.IsNullOrEmpty(filePath) ? Path.GetFileName(filePath) : "Unknown";
                
                // קריאה לפרוצדורה AddDbErrors הקיימת
                connection.Execute(
                    "AddDbErrors",
                    new
                    {
                        user = Environment.UserName ?? "SYSTEM",
                        errnum = "CRITICAL_ERROR",
                        errdesc = message,
                        modulname = fileName,
                        objectname = reportName ?? string.Empty,
                        errline = lineNumber,
                        strinrow = ex?.GetType().Name ?? "CriticalError",
                        moduletype = 1, // Critical level
                        moredtls = moreDetails,
                        comp = Environment.MachineName ?? "Unknown",
                        Ver = GetAssemblyVersion(),
                        CallStack = ex?.StackTrace ?? Environment.StackTrace ?? string.Empty,
                        jobnum = 0,
                        subname = methodName ?? "Unknown"
                    },
                    commandType: CommandType.StoredProcedure);
            }
            catch (Exception)
            {
                // אם נכשל רישום לDB, נזרוק את השגיאה הלאה
                throw;
            }
        }

        /// <summary>
        /// הכנת מחרוזת פרטים נוספים לרישום בDB
        /// </summary>
        private static string PrepareMoreDetails(Dictionary<string, object> parameters, Exception ex)
        {
            var details = new List<string>();
            
            // הוספת פרמטרים שהועברו
            if (parameters != null && parameters.Count > 0)
            {
                details.Add("Parameters:");
                foreach (var param in parameters)
                {
                    string value = param.Value?.ToString() ?? "NULL";
                    // הגבלת אורך כדי לא לעמוס על הDB
                    if (value.Length > 200)
                    {
                        value = value.Substring(0, 200) + "...";
                    }
                    details.Add($"  {param.Key}: {value}");
                }
            }
            
            // הוספת פרטי החריגה
            if (ex != null)
            {
                details.Add($"Exception Type: {ex.GetType().FullName}");
                details.Add($"Exception Message: {ex.Message}");
                
                if (ex.InnerException != null)
                {
                    details.Add($"Inner Exception: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
                }
                
                // הוספת כמה שורות ראשונות מה-StackTrace
                if (!string.IsNullOrEmpty(ex.StackTrace))
                {
                    var stackLines = ex.StackTrace.Split('\n');
                    details.Add("Stack Trace (first 3 lines):");
                    for (int i = 0; i < Math.Min(3, stackLines.Length); i++)
                    {
                        details.Add($"  {stackLines[i].Trim()}");
                    }
                }
            }
            
            // הוספת מידע סביבה
            details.Add($"Machine: {Environment.MachineName}");
            details.Add($"User: {Environment.UserName}");
            details.Add($"Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
            
            return string.Join(Environment.NewLine, details);
        }

        /// <summary>
        /// קבלת גרסת האסמבלי הנוכחי
        /// </summary>
        private static string GetAssemblyVersion()
        {
            try
            {
                return Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0.0";
            }
            catch
            {
                return "1.0.0.0";
            }
        }
    }
}