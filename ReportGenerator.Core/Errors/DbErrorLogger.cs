using System;
using System.Data;
using System.IO;
using Microsoft.Data.SqlClient;

namespace ReportGenerator.Core.Errors
{
    /// <summary>
    /// מחלקה לרישום שגיאות לבסיס הנתונים
    /// </summary>
    public static class DbErrorLogger
    {
        private static string _connectionString;
        private static string _logFolder;
        private static bool _initialized = false;

        /// <summary>
        /// אתחול מחלקת הרישום עם מחרוזת התחברות
        /// </summary>
        /// <param name="connectionString">מחרוזת התחברות</param>
        /// <param name="logFolder">תיקיית לוגים לגיבוי (אופציונלי)</param>
        public static void Initialize(string connectionString, string logFolder = null)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            
            if (!string.IsNullOrEmpty(logFolder))
            {
                _logFolder = logFolder;
                if (!Directory.Exists(_logFolder))
                {
                    try
                    {
                        Directory.CreateDirectory(_logFolder);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"שגיאה ביצירת תיקיית לוגים: {ex.Message}");
                        _logFolder = null;
                    }
                }
            }
            
            _initialized = true;
        }

        /// <summary>
        /// רישום שגיאה לבסיס הנתונים
        /// </summary>
        /// <param name="error">אובייקט שגיאה</param>
        public static void LogError(ErrorContext error)
        {
            if (!_initialized)
            {
                throw new InvalidOperationException("DbErrorLogger must be initialized with a connection string before use.");
            }

            try
            {
                using (SqlConnection connection = new SqlConnection(_connectionString))
                {
                    connection.Open();
                    using (SqlCommand command = new SqlCommand("AddDbErrors", connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;

                        // המרת פרטי השגיאה לפרמטרים של הפרוצדורה
                        command.Parameters.Add("@user", SqlDbType.NVarChar, 50).Value = 
                            error.User ?? Environment.UserName;
                            
                        command.Parameters.Add("@errnum", SqlDbType.NText).Value = 
                            error.ErrorCode;
                            
                        command.Parameters.Add("@errdesc", SqlDbType.NVarChar, 255).Value = 
                            (error.Description?.Length > 255 ? error.Description.Substring(0, 255) : error.Description) ?? "";
                            
                        command.Parameters.Add("@modulname", SqlDbType.NVarChar, 50).Value = 
                            error.ModuleName ?? "";
                            
                        command.Parameters.Add("@objectname", SqlDbType.NVarChar, 50).Value = 
                            error.ReportName ?? "";
                            
                        command.Parameters.Add("@errline", SqlDbType.Int).Value = 
                            error.LineNumber;
                            
                        command.Parameters.Add("@strinrow", SqlDbType.NText).Value = 
                            error.Severity.ToString();
                            
                        command.Parameters.Add("@moduletype", SqlDbType.Int).Value = 
                            (int)error.Severity;
                            
                        command.Parameters.Add("@moredtls", SqlDbType.NVarChar, -1).Value = 
                            error.AdditionalDetails ?? "";
                            
                        command.Parameters.Add("@comp", SqlDbType.NVarChar, 100).Value = 
                            error.MachineName ?? Environment.MachineName;
                            
                        command.Parameters.Add("@Ver", SqlDbType.NVarChar, 100).Value = 
                            error.AppVersion ?? "1.0";
                            
                        command.Parameters.Add("@CallStack", SqlDbType.NVarChar, -1).Value = 
                            error.OriginalException?.StackTrace ?? "";
                            
                        command.Parameters.Add("@jobnum", SqlDbType.Int).Value = 
                            error.JobNumber;
                            
                        command.Parameters.Add("@subname", SqlDbType.NVarChar, 100).Value = 
                            error.MethodName ?? "";

                        command.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                // אם נכשל רישום למסד הנתונים, נרשום לקובץ
                LogToFile(error, ex);
                
                // אם זו שגיאה קריטית, הודעה בקונסולה
                if (error.Severity >= ErrorSeverity.Critical)
                {
                    Console.WriteLine($"שגיאה קריטית - לא ניתן לרשום למסד הנתונים: {error.ErrorCode} - {error.Description}");
                }
            }
        }
        
        /// <summary>
        /// רישום שגיאה לקובץ טקסט (במקרה שרישום לDB נכשל)
        /// </summary>
        /// <param name="error">אובייקט שגיאה</param>
        /// <param name="loggingException">חריגה שהתרחשה בזמן הניסיון לרשום לDB</param>
        private static void LogToFile(ErrorContext error, Exception loggingException = null)
        {
            try
            {
                if (string.IsNullOrEmpty(_logFolder))
                {
                    // אם לא הוגדרה תיקייה, נשתמש בתיקייה זמנית
                    _logFolder = Path.Combine(Path.GetTempPath(), "ReportGenerator", "Logs");
                    if (!Directory.Exists(_logFolder))
                    {
                        Directory.CreateDirectory(_logFolder);
                    }
                }
                
                string logFile = Path.Combine(_logFolder, $"ErrorLog_{DateTime.Now:yyyyMMdd}.txt");
                
                using (StreamWriter writer = new StreamWriter(logFile, true))
                {
                    writer.WriteLine($"--- {DateTime.Now:yyyy-MM-dd HH:mm:ss} ---");
                    writer.WriteLine($"ErrorCode: {error.ErrorCode}");
                    writer.WriteLine($"Severity: {error.Severity}");
                    writer.WriteLine($"Description: {error.Description}");
                    writer.WriteLine($"Module: {error.ModuleName}, Method: {error.MethodName}, Line: {error.LineNumber}");
                    writer.WriteLine($"Report: {error.ReportName}, Job: {error.JobNumber}");
                    writer.WriteLine($"User: {error.User}, Machine: {error.MachineName}");
                    
                    if (error.OriginalException != null)
                    {
                        writer.WriteLine("Original Exception:");
                        writer.WriteLine(error.OriginalException.ToString());
                    }
                    
                    if (loggingException != null)
                    {
                        writer.WriteLine("Logging Exception (failed to write to DB):");
                        writer.WriteLine(loggingException.ToString());
                    }
                    
                    writer.WriteLine(new string('-', 80));
                }
            }
            catch (Exception ex)
            {
                // במקרה של שגיאה ברישום לקובץ, רישום לקונסולה בלבד
                Console.WriteLine($"שגיאה קריטית - לא ניתן לרשום למסד הנתונים או לקובץ לוג: {ex.Message}");
                Console.WriteLine($"פרטי השגיאה המקורית: {error.ErrorCode} - {error.Description}");
            }
        }
    }
}
