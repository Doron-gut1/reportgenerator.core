using System;
using System.Data;
using Microsoft.Data.SqlClient;

namespace ReportGenerator.Core.Errors
{
    /// <summary>
    /// מחלקה לרישום שגיאות לבסיס הנתונים
    /// </summary>
    public static class DbErrorLogger
    {
        private static string _connectionString;

        /// <summary>
        /// אתחול מחלקת הרישום עם מחרוזת התחברות
        /// </summary>
        public static void Initialize(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        }

        /// <summary>
        /// רישום שגיאה לבסיס הנתונים
        /// </summary>
        public static void LogError(ErrorContext error)
        {
            if (string.IsNullOrEmpty(_connectionString))
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
                            (error.Description?.Length > 255 ? error.Description.Substring(0, 255) : error.Description);
                            
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
                // כאן צריך לרשום את השגיאה במקום אחר (קובץ לוג, Console וכד')
                Console.WriteLine($"Error logging to DB: {ex.Message}");
            }
        }
    }
}