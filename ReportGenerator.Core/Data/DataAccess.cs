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
        /// מקבל מיפויים של שמות עמודות לכותרות בעברית עבור פרוצדורות ספציפיות
        /// </summary>
        /// <param name="procNames">שמות הפרוצדורות</param>
        /// <returns>מילון עם המיפויים מאנגלית לעברית</returns>
        public async Task<Dictionary<string, string>> GetColumnMappings(string procNames)
        {
            Dictionary<string, string> mappings = new();
            
            using var connection = new SqlConnection(_connectionString);
            
            // קבלת כל המיפויים הרלוונטיים מהטבלה
            var results = await connection.QueryAsync<ColumnMapping>(
                @"SELECT ColumnName, HebrewAlias, SpecificProcName, SpecificAlias 
                  FROM ReportsGeneratorColumns 
                  WHERE SpecificProcName IS NULL OR SpecificProcName IN @ProcNames",
                new { ProcNames = procNames.Split(';').Select(p => p.Trim()).ToArray() });
            
            // תהליך המיפוי - עדיפות למיפוי ספציפי
            foreach (var mapping in results)
            {
                // אם יש מיפוי ספציפי לפרוצדורה ולעמודה
                if (!string.IsNullOrEmpty(mapping.SpecificProcName) && 
                    !string.IsNullOrEmpty(mapping.SpecificAlias))
                {
                    mappings[mapping.ColumnName] = mapping.SpecificAlias;
                }
                // אחרת להשתמש במיפוי הכללי
                else if (!mappings.ContainsKey(mapping.ColumnName))
                {
                    mappings[mapping.ColumnName] = mapping.HebrewAlias;
                }
            }
            
            return mappings;
        }

        /// <summary>
        /// בדיקה אם יש פרמטרים לפרוצדורה מאוחסנת
        /// </summary>
        /// <param name="procName">שם הפרוצדורה</param>
        /// <returns>רשימת פרמטרים</returns>
        private async Task<IEnumerable<ParameterInfo>> GetProcedureParameters(string procName)
        {
            using var connection = new SqlConnection(_connectionString);
            return await connection.QueryAsync<ParameterInfo>(
                @"SELECT 
                    p.name as Name,
                    t.name as DataType,
                    CASE 
                        WHEN p.has_default_value = 1 THEN 'YES'  -- אם יש ערך ברירת מחדל
                        WHEN definition LIKE '%' + p.name + '%=' THEN 'YES'  -- בדיקה בהגדרת הפרוצדורה
                        ELSE 'NO' 
                    END as IsNullable,
                    p.default_value as DefaultValue,
                    p.parameter_id as ParameterOrder
                FROM sys.parameters p
                INNER JOIN sys.types t ON p.system_type_id = t.system_type_id
                INNER JOIN sys.procedures sp ON p.object_id = sp.object_id
                LEFT JOIN sys.sql_modules m ON sp.object_id = m.object_id
                WHERE sp.name = @ProcName
                ORDER BY p.parameter_id",
                new { ProcName = procName });
        }

        /// <summary>
        /// הרצת מספר פרוצדורות מאוחסנות ומיזוג התוצאות
        /// </summary>
        /// <param name="storedProcNames">שמות הפרוצדורות (מופרדות בפסיק נקודה)</param>
        /// <param name="parameters">פרמטרים להעברה לפרוצדורות</param>
        /// <returns>מילון המכיל DataTable לכל פרוצדורה</returns>
        public async Task<Dictionary<string, DataTable>> ExecuteMultipleStoredProcedures(
            string storedProcNames, 
            Dictionary<string, ParamValue> parameters)
        {
            var procNames = storedProcNames.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            var result = new Dictionary<string, DataTable>();

            foreach (var procName in procNames)
            {
                try
                {
                    // הרצת הפרוצדורה עם הפרמטרים
                    var procData = await ExecuteStoredProcedure(procName.Trim(), parameters);
                    result.Add(procName.Trim(), procData);
                }
                catch (Exception ex)
                {
                    throw new Exception($"Error executing stored procedure {procName}: {ex.Message}", ex);
                }
            }

            return result;
        }

        /// <summary>
        /// הרצת פרוצדורה מאוחסנת וקבלת התוצאות כטבלה
        /// </summary>
        private async Task<DataTable> ExecuteStoredProcedure(string spName, Dictionary<string, ParamValue> parameters)
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
        /// מיזוג טבלאות נתונים
        /// </summary>
        private void MergeDataTables(DataTable mainTable, DataTable newData)
        {
            // אם הטבלה הראשית ריקה, נוסיף את כל העמודות
            if (mainTable.Columns.Count == 0)
            {
                foreach (DataColumn col in newData.Columns)
                {
                    mainTable.Columns.Add(col.ColumnName, col.DataType);
                }
            }
            // אם יש עמודות חדשות, נוסיף אותן
            else
            {
                foreach (DataColumn col in newData.Columns)
                {
                    string columnName = col.ColumnName;
                    if (!mainTable.Columns.Contains(columnName))
                    {
                        mainTable.Columns.Add(columnName, col.DataType);
                    }
                }
            }

            // העתקת כל השורות מהטבלה החדשה
            foreach (DataRow newRow in newData.Rows)
            {
                var row = mainTable.NewRow();
                foreach (DataColumn col in newData.Columns)
                {
                    string columnName = col.ColumnName;
                    row[columnName] = newRow[columnName];
                }
                mainTable.Rows.Add(row);
            }
        }

        /// <summary>
        /// מציאת שם עמודה חלופי במקרה של התנגשות
        /// </summary>
        private string FindMatchingColumnName(DataTable table, string baseColumnName)
        {
            int suffix = 1;
            string columnName = baseColumnName;

            while (table.Columns.Contains(columnName))
            {
                columnName = $"{baseColumnName}_{++suffix}";
            }

            return columnName;
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

    /// <summary>
    /// מידע על פרמטר של פרוצדורה מאוחסנת
    /// </summary>
    public class ParameterInfo
    {
        public string Name { get; set; }
        public string DataType { get; set; }
        public string Mode { get; set; }
        public string IsNullable { get; set; }
        public string DefaultValue { get; set; }

        public bool IsOptional => !string.IsNullOrEmpty(DefaultValue) || IsNullable == "YES";

        public DbType GetDbType()
        {
            return DataType.ToLower() switch
            {
                "varchar" => DbType.String,
                "nvarchar" => DbType.String,
                "int" => DbType.Int32,
                "decimal" => DbType.Decimal,
                "datetime" => DbType.DateTime,
                "bit" => DbType.Boolean,
                "float" => DbType.Double,
                "bigint" => DbType.Int64,
                "smallint" => DbType.Int16,
                "date" => DbType.Date,
                "time" => DbType.Time,
                "money" => DbType.Currency,
                _ => DbType.String
            };
        }
    }
}
