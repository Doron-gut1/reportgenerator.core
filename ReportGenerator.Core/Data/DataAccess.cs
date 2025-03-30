using Dapper;
using Microsoft.Data.SqlClient;
using ReportGenerator.Core.Data.Models;
using System.Data;

namespace ReportGenerator.Core.Data
{
    public class DataAccess
    {
        private readonly string _connectionString;

        public DataAccess(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        }

        /// <summary>
        /// מקבל את שמות הפרוצדורות לשליפת נתונים עבור דוח
        /// </summary>
        /// <param name="reportName">שם הדוח</param>
        /// <returns>מחרוזת עם שמות הפרוצדורות (מופרדות בפסיק נקודה)</returns>
        public async Task<string> GetStoredProcName(string reportName)
        {
            using var connection = new SqlConnection(_connectionString);
            var result = await connection.QuerySingleOrDefaultAsync<string>(
                "SELECT StoredProcName FROM ReportsGenerator WHERE ReportName = @ReportName",
                new { ReportName = reportName });

            return result ?? throw new Exception($"Report Name {reportName} not found");
        }

        /// <summary>
        /// מקבל את כותרת הדוח
        /// </summary>
        /// <param name="reportName">שם הדוח</param>
        /// <returns>כותרת הדוח</returns>
        public async Task<string> GetReportTitle(string reportName)
        {
            using var connection = new SqlConnection(_connectionString);
            var result = await connection.QuerySingleOrDefaultAsync<string>(
                "SELECT Title FROM ReportsGenerator WHERE ReportName = @ReportName",
                new { ReportName = reportName });

            return result ?? throw new Exception($"Report Name {reportName} not found");
        }

        /// <summary>
        /// מקבל את כל המידע על דוח מטבלת ההגדרות
        /// </summary>
        /// <param name="reportName">שם הדוח</param>
        /// <returns>אובייקט עם מידע על הדוח</returns>
        public async Task<ReportConfig> GetReportConfig(string reportName)
        {
            using var connection = new SqlConnection(_connectionString);
            var result = await connection.QuerySingleOrDefaultAsync<ReportConfig>(
                "SELECT ReportID, ReportName, StoredProcName, Title, Description " +
                "FROM ReportsGenerator WHERE ReportName = @ReportName",
                new { ReportName = reportName });

            return result ?? throw new Exception($"Report Name {reportName} not found");
        }

        /// <summary>
        /// בודק אם האובייקט הוא פונקציה טבלאית
        /// </summary>
        /// <param name="objectName">שם האובייקט</param>
        /// <returns>האם האובייקט הוא פונקציה טבלאית</returns>
        public async Task<bool> IsTableFunction(string objectName)
        {
            using var connection = new SqlConnection(_connectionString);
            
            // הסרת התחילית dbo. אם קיימת
            string cleanName = objectName.StartsWith("dbo.") 
                ? objectName.Substring(4) 
                : objectName;
            
            var result = await connection.ExecuteScalarAsync<int>(
                @"SELECT COUNT(*) FROM sys.objects 
                  WHERE name = @Name 
                  AND type IN ('IF', 'TF', 'FT')",  // סוגים שונים של פונקציות טבלאיות
                new { Name = cleanName });
            
            return result > 0;
        }

        /// <summary>
        /// מקבל מיפויים של שמות עמודות לכותרות בעברית
        /// </summary>
        /// <param name="procNames">שמות הפרוצדורות/פונקציות</param>
        /// <returns>מילון עם המיפויים מאנגלית לעברית</returns>
        public async Task<Dictionary<string, string>> GetColumnMappings(string procNames)
        {
            Dictionary<string, string> mappings = new();
            
            // פיצול שמות הפרוצדורות
            var procNamesList = procNames.Split(';')
                .Select(p => p.Trim())
                .ToList();
            
            using var connection = new SqlConnection(_connectionString);
            
            // מיפויים לפי שם פרוצדורה (לשדות מחושבים)
            var procMappings = await connection.QueryAsync<ColumnMapping>(
                @"SELECT TableName, ColumnName, HebrewAlias
                  FROM ReportsGeneratorColumns 
                  WHERE TableName IN @ProcNames",
                new { ProcNames = procNamesList });
            
            // מיפויים לפי טבלה (לשדות מטבלה)
            var tableMappings = await connection.QueryAsync<ColumnMapping>(
                @"SELECT TableName, ColumnName, HebrewAlias
                  FROM ReportsGeneratorColumns 
                  WHERE TableName IN 
                    (SELECT name FROM sys.tables)");
            
            // הוספת מיפויים לפי פרוצדורה (שדות מחושבים)
            foreach (var mapping in procMappings)
            {
                // מיפוי ישיר לפי שם השדה
                mappings[mapping.ColumnName] = mapping.HebrewAlias;
            }
            
            // הוספת מיפויים לפי טבלה (שדות מטבלה)
            foreach (var mapping in tableMappings)
            {
                // מיפוי בפורמט TableName_ColumnName
                string key = $"{mapping.TableName}_{mapping.ColumnName}";
                mappings[key] = mapping.HebrewAlias;
            }
            
            return mappings;
        }

        /// <summary>
        /// הרצת פונקציה טבלאית וקבלת התוצאות
        /// </summary>
        /// <param name="functionName">שם הפונקציה</param>
        /// <param name="parameters">פרמטרים</param>
        /// <returns>טבלת נתונים עם התוצאות</returns>
        public async Task<DataTable> ExecuteTableFunction(string functionName, Dictionary<string, ParamValue> parameters)
        {
            using var connection = new SqlConnection(_connectionString);
            
            // בניית מחרוזת הפרמטרים
            List<string> paramList = new List<string>();
            foreach (var param in parameters)
            {
                paramList.Add($"@{param.Key}");
            }
            
            string paramString = string.Join(", ", paramList);
            
            // בניית פקודת SQL - שימוש בפונקציה כחלק מ-SELECT
            string sql = $"SELECT * FROM {functionName}({paramString})";
            
            // ביצוע השאילתה
            var result = await connection.QueryAsync(
                sql,
                parameters.ToDictionary(
                    kvp => kvp.Key, 
                    kvp => kvp.Value.Value ?? DBNull.Value),
                commandType: CommandType.Text);
            
            return ToDataTable(result);
        }

        /// <summary>
        /// הרצת מספר פרוצדורות מאוחסנות ו/או פונקציות טבלאיות
        /// </summary>
        /// <param name="storedProcNames">שמות הפרוצדורות/פונקציות (מופרדות בפסיק נקודה)</param>
        /// <param name="parameters">פרמטרים להעברה</param>
        /// <returns>מילון המכיל DataTable לכל פרוצדורה/פונקציה</returns>
        public async Task<Dictionary<string, DataTable>> ExecuteMultipleStoredProcedures(
            string storedProcNames, 
            Dictionary<string, ParamValue> parameters)
        {
            var procNames = storedProcNames.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            var result = new Dictionary<string, DataTable>();

            foreach (var procName in procNames)
            {
                string trimmedName = procName.Trim();
                
                try
                {
                    // בדיקה אם מדובר בפונקציה טבלאית או בפרוצדורה רגילה
                    bool isFunction = trimmedName.StartsWith("dbo.") || 
                                     await IsTableFunction(trimmedName);
                    
                    DataTable procData;
                    if (isFunction)
                    {
                        // הרצת פונקציה טבלאית
                        procData = await ExecuteTableFunction(trimmedName, parameters);
                    }
                    else
                    {
                        // הרצת פרוצדורה רגילה
                        procData = await ExecuteStoredProcedure(trimmedName, parameters);
                    }
                    
                    // הוספה למילון התוצאות
                    result.Add(trimmedName, procData);
                }
                catch (Exception ex)
                {
                    throw new Exception($"Error executing {trimmedName}: {ex.Message}", ex);
                }
            }

            return result;
        }

        /// <summary>
        /// הרצת פרוצדורה מאוחסנת וקבלת התוצאות כטבלה
        /// </summary>
        public async Task<DataTable> ExecuteStoredProcedure(string spName, Dictionary<string, ParamValue> parameters)
        {
            using var connection = new SqlConnection(_connectionString);

            var dynamicParams = new DynamicParameters();
            foreach (var param in parameters)
            {
                dynamicParams.Add(
                    param.Key,
                    param.Value.Value ?? DBNull.Value,
                    param.Value.Type);
            }

            var result = await connection.QueryAsync(
                spName,
                dynamicParams,
                commandType: CommandType.StoredProcedure);

            return ToDataTable(result);
        }

        /// <summary>
        /// המרת תוצאות Dapper לטבלת נתונים
        /// </summary>
        private DataTable ToDataTable(IEnumerable<dynamic> data)
        {
            var dt = new DataTable();
            var first = true;

            foreach (var row in data)
            {
                IDictionary<string, object> dict = row as IDictionary<string, object>;
                if (first)
                {
                    first = false;
                    foreach (var col in dict.Keys)
                    {
                        dt.Columns.Add(col);
                    }
                }

                var newRow = dt.NewRow();
                foreach (var col in dict.Keys)
                {
                    newRow[col] = dict[col] ?? DBNull.Value;
                }
                dt.Rows.Add(newRow);
            }

            return dt;
        }
    }
}
