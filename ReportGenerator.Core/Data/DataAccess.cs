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


        public async Task<string> GetMonthName(int mnt)
        {
            using var connection = new SqlConnection(_connectionString);
            return await connection.QuerySingleOrDefaultAsync<string>(
                "SELECT dbo.mntname(@Mnt)",
                new { Mnt = mnt });
        }

        public async Task<string> GetSugtsName(int sugts)
        {
            using var connection = new SqlConnection(_connectionString);
            var result = await connection.QuerySingleOrDefaultAsync<string>(
                "SELECT dbo.SugtsName(@Sugts)",
                new { Sugts = sugts });

            return result ?? $"סוג חיוב {sugts}"; // החזרת ברירת מחדל אם לא נמצא ערך
        }

        public async Task<string> GetIshvName(int isvkod)
        {
            using var connection = new SqlConnection(_connectionString);
            try
            {
                // הערה: יש ליצור פונקציית SQL מתאימה (IsvName) או לשלוף ישירות מטבלת יישובים
                var result = await connection.QuerySingleOrDefaultAsync<string>(
                    "SELECT name FROM ishuv WHERE isvkod = @Isvkod",
                    new { Isvkod = isvkod });

                return result ?? $"יישוב {isvkod}"; // החזרת ברירת מחדל אם לא נמצא
            }
            catch (Exception ex)
            {
                Console.WriteLine($"שגיאה בשליפת שם יישוב {isvkod}: {ex.Message}");
                return $"יישוב {isvkod}"; // החזרת ברירת מחדל במקרה של שגיאה
            }
        }

        /// <summary>
        /// מאפשר שליפת שמות מרובים לקודים (למקרה של רשימות)
        /// </summary>
        /// <param name="codes">רשימת קודים מופרדים בפסיקים</param>
        /// <param name="tableName">שם הטבלה לשליפה</param>
        /// <param name="codeField">שם שדה הקוד</param>
        /// <param name="nameField">שם שדה השם</param>
        /// <returns>מחרוזת עם השמות מופרדים בפסיקים</returns>
        public async Task<string> GetCodeNames(string codes, string tableName, string codeField, string nameField)
        {
            if (string.IsNullOrEmpty(codes))
                return "הכל";

            using var connection = new SqlConnection(_connectionString);
            try
            {
                // פירוק מחרוזת הקודים
                var codesList = codes.Split(',')
                    .Select(x => x.Trim())
                    .Where(x => !string.IsNullOrEmpty(x))
                    .Select(x => int.Parse(x))
                    .ToList();

                if (!codesList.Any())
                    return "הכל";

                // יצירת פרמטר טבלאי
                var table = new DataTable();
                table.Columns.Add("Value", typeof(int));

                foreach (var code in codesList)
                {
                    table.Rows.Add(code);
                }

                // שליפת השמות
                var query = $@"
            SELECT {nameField}
            FROM {tableName}
            WHERE {codeField} IN (SELECT Value FROM @Codes)
            ORDER BY {nameField}";

                var names = await connection.QueryAsync<string>(
                    query,
                    new { Codes = table.AsTableValuedParameter("IntList") });

                // הרכבת מחרוזת התוצאה
                return string.Join(", ", names);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"שגיאה בשליפת שמות עבור קודים {codes}: {ex.Message}");
                return codes; // החזרת הקודים המקוריים במקרה של שגיאה
            }
        }

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
        /// בודק אם אובייקט SQL הוא פונקציה טבלאית
        /// </summary>
        /// <param name="objectName">שם האובייקט ב-SQL</param>
        /// <returns>האם האובייקט הוא פונקציה טבלאית</returns>
        public async Task<bool> IsTableFunction(string objectName)
        {
            using var connection = new SqlConnection(_connectionString);
            var count = await connection.ExecuteScalarAsync<int>(
                @"SELECT COUNT(1) FROM sys.objects 
                  WHERE name = @Name
                  AND type IN ('IF', 'TF', 'FT')",  // IF = inline function, TF = table function, FT = CLR table-function
                new { Name = objectName.Replace("dbo.", "") });
            
            return count > 0;
        }

        /// <summary>
        /// מקבל מיפויים של שמות עמודות לכותרות בעברית
        /// </summary>
        /// <param name="procNames">שמות הפרוצדורות/פונקציות</param>
        /// <returns>מילון עם המיפויים מאנגלית לעברית</returns>
        public async Task<Dictionary<string, string>> GetColumnMappings(string procNames)
        {
            Dictionary<string, string> mappings = new(StringComparer.OrdinalIgnoreCase);
            
            using var connection = new SqlConnection(_connectionString);
            
            // פיצול שמות הפרוצדורות/פונקציות
            var procsList = procNames.Split(';')
                                    .Select(p => p.Trim())
                                    .Select(p => p.Replace("dbo.", ""))
                                    .ToArray();
            
            // קבלת כל המיפויים הרלוונטיים בשאילתה אחת
            var results = await connection.QueryAsync<ColumnMapping>(
                @"SELECT TableName, ColumnName, HebrewAlias 
                  FROM ReportsGeneratorColumns 
                  WHERE TableName IN @ProcNames OR TableName IN (SELECT name FROM sys.tables)",
                new { ProcNames = procsList });
            
            foreach (var mapping in results)
            {
                // שמירת המיפוי בהתאם לסוג השדה:
                // 1. לשדות מטבלה (TableName_ColumnName)
                if (mapping.TableName != procNames)
                {
                    string compositeKey = $"{mapping.TableName}_{mapping.ColumnName}";
                    mappings[compositeKey] = mapping.HebrewAlias;
                }
                
                // 2. לשדות מחושבים (שם השדה בלבד)
                if (procsList.Contains(mapping.TableName))
                {
                    mappings[mapping.ColumnName] = mapping.HebrewAlias;
                }
            }
            
            return mappings;
        }

        /// <summary>
        /// הרצת מספר פרוצדורות או פונקציות טבלאיות ומיזוג התוצאות
        /// </summary>
        /// <param name="objectNames">שמות הפרוצדורות/פונקציות (מופרדות בפסיק נקודה)</param>
        /// <param name="parameters">פרמטרים להעברה לפרוצדורות</param>
        /// <returns>מילון המכיל DataTable לכל פרוצדורה/פונקציה</returns>
        public async Task<Dictionary<string, DataTable>> ExecuteMultipleStoredProcedures(
            string objectNames, 
            Dictionary<string, ParamValue> parameters)
        {
            var objects = objectNames.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            var result = new Dictionary<string, DataTable>();

            foreach (var objectName in objects)
            {
                try
                {
                    string trimmedName = objectName.Trim();
                    
                    // בדיקה אם מדובר בפונקציה או בפרוצדורה
                    bool isFunction = trimmedName.StartsWith("dbo.") || 
                                     await IsTableFunction(trimmedName);
                    
                    DataTable data;
                    if (isFunction)
                    {
                        // הרצת פונקציה טבלאית
                        data = await ExecuteTableFunction(trimmedName, parameters);
                    }
                    else
                    {
                        // הרצת פרוצדורה רגילה
                        data = await ExecuteStoredProcedure(trimmedName, parameters);
                    }
                    
                    // הוספה למילון התוצאות
                    result.Add(trimmedName, data);
                }
                catch (Exception ex)
                {
                    throw new Exception($"Error executing {objectName}: {ex.Message}", ex);
                }
            }

            return result;
        }

        /// <summary>
        /// הרצת פונקציה טבלאית
        /// </summary>
        /// <param name="functionName">שם הפונקציה</param>
        /// <param name="parameters">פרמטרים</param>
        /// <returns>טבלת נתונים עם התוצאות</returns>
        public async Task<DataTable> ExecuteTableFunction(string functionName, Dictionary<string, ParamValue> parameters)
        {
            using var connection = new SqlConnection(_connectionString);
            
            // פתיחת החיבור
            await connection.OpenAsync();
            
            // הכנת מחרוזת הפרמטרים
            var paramList = new List<string>();
            foreach (var param in parameters)
            {
                paramList.Add($"@{param.Key}");
            }
            
            string paramString = string.Join(", ", paramList);
            
            // בניית פקודת SQL
            string sql = string.IsNullOrEmpty(paramString) 
                ? $"SELECT * FROM {functionName}()"
                : $"SELECT * FROM {functionName}({paramString})";
            
            // הכנת הפקודה
            using var command = new SqlCommand(sql, connection);
            
            // הוספת פרמטרים
            foreach (var param in parameters)
            {
                // המרה לסוג הפרמטר המתאים
                SqlDbType sqlType = GetSqlDbType(param.Value.Type);
                
                var sqlParam = new SqlParameter($"@{param.Key}", sqlType)
                {
                    Value = param.Value.Value ?? DBNull.Value
                };
                
                command.Parameters.Add(sqlParam);
            }
            
            // הרצת הפקודה
            using var reader = await command.ExecuteReaderAsync();
            
            // המרה ל-DataTable
            var dataTable = new DataTable();
            dataTable.Load(reader);
            
            return dataTable;
        }

        /// <summary>
        /// הרצת פרוצדורה מאוחסנת וקבלת התוצאות כטבלה
        /// </summary>
        /// <param name="spName">שם הפרוצדורה</param>
        /// <param name="parameters">פרמטרים</param>
        /// <returns>טבלת נתונים עם התוצאות</returns>
        private async Task<DataTable> ExecuteStoredProcedure(string spName, Dictionary<string, ParamValue> parameters)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            try
            {
                // שליפת מידע על הפרמטרים שהפרוצדורה מקבלת
                var procParams = await GetProcedureParameters(spName);

                var dynamicParams = new DynamicParameters();

                // רק פרמטרים שנמצאים גם ברשימת הפרמטרים של הפרוצדורה
                foreach (var procParam in procParams)
                {
                    string paramName = procParam.Name;
                    if (paramName.StartsWith("@"))
                        paramName = paramName.Substring(1); // הסרת @ מתחילת השם

                    // בדיקה אם הפרמטר הזה נמצא ברשימת הפרמטרים שהועברו
                    if (parameters.TryGetValue(paramName, out ParamValue paramValue))
                    {
                        dynamicParams.Add(
                            procParam.Name, // שם הפרמטר כפי שמוגדר בפרוצדורה
                            paramValue.Value ?? DBNull.Value,
                            paramValue.Type);

                        //Console.WriteLine($"הוספת פרמטר לפרוצדורה {spName}: {procParam.Name} = {paramValue.Value}");
                    }
                    else if (!procParam.IsOptional)
                    {
                        // אם זה פרמטר חובה שלא הועבר, זרוק שגיאה
                        throw new Exception($"Missing required parameter {procParam.Name} for stored procedure {spName}");
                    }
                }

                // הרצת הפרוצדורה עם הפרמטרים המתאימים בלבד
                var result = await connection.QueryAsync(
                    spName,
                    dynamicParams,
                    commandType: CommandType.StoredProcedure);

                return ToDataTable(result);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error executing stored procedure {spName}: {ex.Message}", ex);
            }
        }

        private async Task<IEnumerable<ParameterInfo>> GetProcedureParameters(string procName)
{
    // הסרת קידומת סכמה אם קיימת
    string cleanProcName = procName;
    if (procName.Contains("."))
    {
        cleanProcName = procName.Substring(procName.LastIndexOf('.') + 1);
    }
    
    using var connection = new SqlConnection(_connectionString);
    var result = await connection.QueryAsync<ParameterInfo>(
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
        new { ProcName = cleanProcName });
    
    //if (!result.Any())
    //{
    //    Console.WriteLine($"לא נמצאו פרמטרים לפרוצדורה {procName}");
    //}
    //else
    //{
    //   // Console.WriteLine($"נמצאו {result.Count()} פרמטרים לפרוצדורה {procName}");
    //    foreach (var param in result)
    //    {
    //        Console.WriteLine($"פרמטר: {param.Name}, סוג: {param.DataType}, אופציונלי: {param.IsOptional}");
    //    }
    //}
    
    return result;
}

        /// <summary>
        /// מיזוג טבלאות נתונים
        /// </summary>
        //private void MergeDataTables(DataTable mainTable, DataTable newData)
        //{
        //    // אם הטבלה הראשית ריקה, נוסיף את כל העמודות
        //    if (mainTable.Columns.Count == 0)
        //    {
        //        foreach (DataColumn col in newData.Columns)
        //        {
        //            mainTable.Columns.Add(col.ColumnName, col.DataType);
        //        }
        //    }
        //    // אם יש עמודות חדשות, נוסיף אותן
        //    else
        //    {
        //        foreach (DataColumn col in newData.Columns)
        //        {
        //            string columnName = col.ColumnName;
        //            if (!mainTable.Columns.Contains(columnName))
        //            {
        //                mainTable.Columns.Add(columnName, col.DataType);
        //            }
        //        }
        //    }

        //    // העתקת כל השורות מהטבלה החדשה
        //    foreach (DataRow newRow in newData.Rows)
        //    {
        //        var row = mainTable.NewRow();
        //        foreach (DataColumn col in newData.Columns)
        //        {
        //            string columnName = col.ColumnName;
        //            row[columnName] = newRow[columnName];
        //        }
        //        mainTable.Rows.Add(row);
        //    }
        //}

        ///// <summary>
        ///// מציאת שם עמודה חלופי במקרה של התנגשות
        ///// </summary>
        //private string FindMatchingColumnName(DataTable table, string baseColumnName)
        //{
        //    int suffix = 1;
        //    string columnName = baseColumnName;

        //    while (table.Columns.Contains(columnName))
        //    {
        //        columnName = $"{baseColumnName}_{++suffix}";
        //    }

        //    return columnName;
        //}

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
        
        /// <summary>
        /// המרה מ-DbType ל-SqlDbType
        /// </summary>
        private SqlDbType GetSqlDbType(DbType dbType)
        {
            return dbType switch
            {
                DbType.AnsiString => SqlDbType.VarChar,
                DbType.AnsiStringFixedLength => SqlDbType.Char,
                DbType.Binary => SqlDbType.VarBinary,
                DbType.Boolean => SqlDbType.Bit,
                DbType.Byte => SqlDbType.TinyInt,
                DbType.Currency => SqlDbType.Money,
                DbType.Date => SqlDbType.Date,
                DbType.DateTime => SqlDbType.DateTime,
                DbType.DateTime2 => SqlDbType.DateTime2,
                DbType.DateTimeOffset => SqlDbType.DateTimeOffset,
                DbType.Decimal => SqlDbType.Decimal,
                DbType.Double => SqlDbType.Float,
                DbType.Guid => SqlDbType.UniqueIdentifier,
                DbType.Int16 => SqlDbType.SmallInt,
                DbType.Int32 => SqlDbType.Int,
                DbType.Int64 => SqlDbType.BigInt,
                DbType.Object => SqlDbType.Variant,
                DbType.SByte => SqlDbType.TinyInt,
                DbType.Single => SqlDbType.Real,
                DbType.String => SqlDbType.NVarChar,
                DbType.StringFixedLength => SqlDbType.NChar,
                DbType.Time => SqlDbType.Time,
                DbType.UInt16 => SqlDbType.SmallInt,
                DbType.UInt32 => SqlDbType.Int,
                DbType.UInt64 => SqlDbType.BigInt,
                DbType.VarNumeric => SqlDbType.Decimal,
                DbType.Xml => SqlDbType.Xml,
                _ => SqlDbType.NVarChar,
            };
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