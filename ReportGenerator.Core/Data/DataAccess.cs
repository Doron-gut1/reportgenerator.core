using Dapper;
using Microsoft.Data.SqlClient;
using ReportGenerator.Core.Data.Models;
using ReportGenerator.Core.Errors;
using ReportGenerator.Core.Services;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

namespace ReportGenerator.Core.Data
{
    public class DataAccess : IDataAccess
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
            try
            {
                using var connection = new SqlConnection(_connectionString);
                var result = await connection.QuerySingleOrDefaultAsync<string>(
                    "SELECT StoredProcName FROM ReportsGenerator WHERE ReportName = @ReportName",
                    new { ReportName = reportName });

                if (result == null)
                {
                    ErrorManager.LogError(
                        ErrorCodes.DB.Report_NotFound, 
                        ErrorSeverity.Critical,
                        $"דוח בשם {reportName} לא נמצא במערכת");
                    throw new Exception($"Report Name {reportName} not found");
                }

                return result;
            }
            catch (SqlException ex)
            {
                ErrorManager.LogError(
                    ErrorCodes.DB.Query_Failed,
                    ErrorSeverity.Critical,
                    $"שגיאת SQL בזמן שליפת הגדרות דוח {reportName}",
                    ex);
                throw new Exception($"SQL error retrieving stored procedures for report {reportName}", ex);
            }
            catch (Exception ex) when (!(ex.InnerException is SqlException) && !(ex is InvalidOperationException))
            {
                ErrorManager.LogError(
                    ErrorCodes.DB.Connection_Failed,
                    ErrorSeverity.Critical,
                    $"שגיאת התחברות למסד נתונים בזמן שליפת הגדרות דוח {reportName}",
                    ex);
                throw new Exception($"Database connection error retrieving stored procedures for report {reportName}", ex);
            }
        }

        /// <summary>
        /// מקבל את שם החודש לפי מספר חודש
        /// </summary>
        public async Task<string> GetMonthName(int mnt)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                var result = await connection.QuerySingleOrDefaultAsync<string>(
                    "SELECT dbo.mntname(@Mnt)",
                    new { Mnt = mnt });
                    
                if (result == null)
                {
                    ErrorManager.LogWarning(
                        ErrorCodes.DB.MonthName_NotFound,
                        $"לא נמצא שם עבור חודש {mnt}");
                    return $"חודש {mnt}";
                }
                
                return result;
            }
            catch (Exception ex)
            {
                ErrorManager.LogNormalError(
                    ErrorCodes.DB.Query_Failed,
                    $"שגיאה בשליפת שם חודש {mnt}",
                    ex);
                return $"חודש {mnt}";
            }
        }

        /// <summary>
        /// מקבל את שם התקופה לפי מספר חודש
        /// </summary>
        public async Task<string> GetPeriodName(int mnt)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                var result = await connection.QuerySingleOrDefaultAsync<string>(
                    "SELECT dbo.PeriodName(@Mnt)",
                    new { Mnt = mnt });
                    
                if (result == null)
                {
                    ErrorManager.LogWarning(
                        ErrorCodes.DB.MonthName_NotFound,
                        $"לא נמצא שם תקופה עבור חודש {mnt}");
                    return $"תקופה {mnt}";
                }
                
                return result;
            }
            catch (Exception ex)
            {
                ErrorManager.LogNormalError(
                    ErrorCodes.DB.Query_Failed,
                    $"שגיאה בשליפת שם תקופה {mnt}",
                    ex);
                return $"תקופה {mnt}";
            }
        }
        public async Task<string> GetMoazaName()
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                var result = await connection.QuerySingleOrDefaultAsync<string>(
                    "SELECT dbo.GetMoazaName()");

                if (result == null)
                {
                    ErrorManager.LogWarning(
                        ErrorCodes.DB.MoazaName_NotFound,
                        "לא נמצא שם מועצה");
                    return "מועצה לא ידועה";
                }

                return result;
            }
            catch (Exception ex)
            {
                ErrorManager.LogNormalError(
                    ErrorCodes.DB.Query_Failed,
                    "שגיאה בשליפת שם מועצה",
                    ex);
                return "מועצה לא ידועה";
            }
        }

        /// <summary>
        /// מקבל את שם סוג החיוב לפי קוד
        /// </summary>
        public async Task<string> GetSugtsName(int sugts)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                var result = await connection.QuerySingleOrDefaultAsync<string>(
                    "SELECT dbo.SugtsName(@Sugts)",
                    new { Sugts = sugts });

                if (result == null)
                {
                    ErrorManager.LogWarning(
                        ErrorCodes.DB.SugtsName_NotFound,
                        $"לא נמצא שם עבור סוג חיוב {sugts}");
                    return $"סוג חיוב {sugts}";
                }
                
                return result;
            }
            catch (Exception ex)
            {
                ErrorManager.LogNormalError(
                    ErrorCodes.DB.Query_Failed,
                    $"שגיאה בשליפת שם סוג חיוב {sugts}",
                    ex);
                return $"סוג חיוב {sugts}";
            }
        }

        /// <summary>
        /// מקבל את שם היישוב לפי קוד
        /// </summary>
        public async Task<string> GetIshvName(int isvkod)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                var result = await connection.QuerySingleOrDefaultAsync<string>(
                    "SELECT dbo.GetCityName(@Isvkod)",
                    new { Isvkod = isvkod });

                if (result == null)
                {
                    ErrorManager.LogWarning(
                        ErrorCodes.DB.IshvName_NotFound,
                        $"לא נמצא שם עבור יישוב {isvkod}");
                    return $"יישוב {isvkod}";
                }
                
                return result;
            }
            catch (Exception ex)
            {
                ErrorManager.LogNormalError(
                    ErrorCodes.DB.Query_Failed,
                    $"שגיאה בשליפת שם יישוב {isvkod}",
                    ex);
                return $"יישוב {isvkod}";
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

            try
            {
                using var connection = new SqlConnection(_connectionString);
                
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
                ErrorManager.LogNormalError(
                    ErrorCodes.DB.Query_Failed,
                    $"שגיאה בשליפת שמות עבור קודים {codes} מטבלה {tableName}",
                    ex);
                return codes;
            }
        }

        /// <summary>
        /// מקבל את הגדרות הדוח
        /// </summary>
        public async Task<ReportConfig> GetReportConfig(string reportName)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                var result = await connection.QuerySingleOrDefaultAsync<ReportConfig>(
                    "SELECT ReportID, ReportName, StoredProcName, Title, Description " +
                    "FROM ReportsGenerator WHERE ReportName = @ReportName",
                    new { ReportName = reportName });

                if (result == null)
                {
                    ErrorManager.LogError(
                        ErrorCodes.DB.Report_Config_Invalid,
                        ErrorSeverity.Critical,
                        $"הגדרות דוח {reportName} לא נמצאו במערכת");
                    throw new Exception($"Report configuration for {reportName} not found");
                }

                return result;
            }
            catch (SqlException ex)
            {
                ErrorManager.LogError(
                    ErrorCodes.DB.Query_Failed,
                    ErrorSeverity.Critical,
                    $"שגיאת SQL בזמן שליפת הגדרות דוח {reportName}",
                    ex);
                throw new Exception($"SQL error retrieving report configuration for {reportName}", ex);
            }
            catch (Exception ex) when (!(ex.InnerException is SqlException) && !(ex is InvalidOperationException))
            {
                ErrorManager.LogError(
                    ErrorCodes.DB.Connection_Failed,
                    ErrorSeverity.Critical,
                    $"שגיאת התחברות למסד נתונים בזמן שליפת הגדרות דוח {reportName}",
                    ex);
                throw new Exception($"Database connection error retrieving report configuration for {reportName}", ex);
            }
        }

        /// <summary>
        /// בודק אם אובייקט SQL הוא פונקציה טבלאית
        /// </summary>
        /// <param name="objectName">שם האובייקט ב-SQL</param>
        /// <returns>האם האובייקט הוא פונקציה טבלאית</returns>
        public async Task<bool> IsTableFunction(string objectName)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                var count = await connection.ExecuteScalarAsync<int>(
                    @"SELECT COUNT(1) FROM sys.objects 
                  WHERE name = @Name
                  AND type IN ('IF', 'TF', 'FT')",  // IF = inline function, TF = table function, FT = CLR table-function
                    new { Name = objectName.Replace("dbo.", "") });
                
                return count > 0;
            }
            catch (Exception ex)
            {
                ErrorManager.LogNormalError(
                    ErrorCodes.DB.Query_Failed,
                    $"שגיאה בבדיקה אם {objectName} הוא פונקציה טבלאית",
                    ex);
                return false;
            }
        }

        /// <summary>
        /// מקבל מיפויים של שמות עמודות לכותרות בעברית
        /// </summary>
        /// <param name="procNames">שמות הפרוצדורות/פונקציות</param>
        /// <returns>מילון עם המיפויים מאנגלית לעברית</returns>
        public async Task<Dictionary<string, string>> GetColumnMappings(string procNames)
        {
            Dictionary<string, string> mappings = new(StringComparer.OrdinalIgnoreCase);
            
            try
            {
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
                
                if (mappings.Count == 0)
                {
                    ErrorManager.LogWarning(
                        ErrorCodes.DB.ColumnMapping_NotFound,
                        $"לא נמצאו מיפויי עמודות עבור פרוצדורות: {procNames}");
                }
                
                return mappings;
            }
            catch (Exception ex)
            {
                ErrorManager.LogNormalError(
                    ErrorCodes.DB.Query_Failed,
                    $"שגיאה בשליפת מיפויי עמודות עבור פרוצדורות: {procNames}",
                    ex);
                return mappings;
            }
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
                    ErrorManager.LogError(
                        ErrorCodes.DB.Query_Failed,
                        ErrorSeverity.Error,
                        $"שגיאה בהרצת {objectName}",
                        ex);
                    
                    // צור טבלה ריקה כדי לא לשבור את התהליך (אם זו לא פרוצדורה קריטית)
                    var emptyTable = new DataTable();
                    emptyTable.Columns.Add("ERROR", typeof(string));
                    emptyTable.Rows.Add($"Error executing {objectName}: {ex.Message}");
                    result.Add(objectName, emptyTable);
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
            try
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
            catch (SqlException ex)
            {
                ErrorManager.LogError(
                    ErrorCodes.DB.TableFunc_Execution_Failed,
                    ErrorSeverity.Error,
                    $"שגיאת SQL בהרצת פונקציה טבלאית {functionName}",
                    ex);
                throw;
            }
            catch (Exception ex)
            {
                ErrorManager.LogError(
                    ErrorCodes.DB.Connection_Failed,
                    ErrorSeverity.Error,
                    $"שגיאת התחברות או הרצה של פונקציה טבלאית {functionName}",
                    ex);
                throw;
            }
        }

        /// <summary>
        /// הרצת פרוצדורה מאוחסנת וקבלת התוצאות כטבלה
        /// </summary>
        /// <param name="spName">שם הפרוצדורה</param>
        /// <param name="parameters">פרמטרים</param>
        /// <returns>טבלת נתונים עם התוצאות</returns>
        private async Task<DataTable> ExecuteStoredProcedure(string spName, Dictionary<string, ParamValue> parameters)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

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
                    }
                    else if (!procParam.IsOptional)
                    {
                        // אם זה פרמטר חובה שלא הועבר, רשום שגיאה
                        ErrorManager.LogError(
                            ErrorCodes.DB.StoredProc_MissingParam,
                            ErrorSeverity.Error,
                            $"פרמטר נדרש {procParam.Name} חסר עבור פרוצדורה {spName}");
                            
                        throw new ArgumentException($"Missing required parameter {procParam.Name} for stored procedure {spName}");
                    }
                }

                // הרצת הפרוצדורה עם הפרמטרים המתאימים בלבד
                var result = await connection.QueryAsync(
                    spName,
                    dynamicParams,
                    commandType: CommandType.StoredProcedure);

                return ToDataTable(result);
            }
            catch (SqlException ex)
            {
                ErrorManager.LogError(
                    ErrorCodes.DB.StoredProc_Execution_Failed,
                    ErrorSeverity.Error,
                    $"שגיאת SQL בהרצת פרוצדורה {spName}",
                    ex);
                throw;
            }
            catch (ArgumentException ex)
            {
                // כבר נרשמה שגיאה בבדיקת הפרמטרים, רק זרוק הלאה
                throw;
            }
            catch (Exception ex)
            {
                ErrorManager.LogError(
                    ErrorCodes.DB.Connection_Failed,
                    ErrorSeverity.Error,
                    $"שגיאת התחברות או הרצה של פרוצדורה {spName}",
                    ex);
                throw;
            }
        }

        /// <summary>
        /// מקבל מידע על פרמטרים של פרוצדורה מאוחסנת
        /// </summary>
        private async Task<IEnumerable<ParameterInfo>> GetProcedureParameters(string procName)
        {
            try
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
                
                return result;
            }
            catch (Exception ex)
            {
                ErrorManager.LogNormalError(
                    ErrorCodes.DB.Query_Failed,
                    $"שגיאה בשליפת מידע על פרמטרים של הפרוצדורה {procName}",
                    ex);
                return Enumerable.Empty<ParameterInfo>();
            }
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