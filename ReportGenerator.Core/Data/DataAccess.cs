using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using ReportGenerator.Core.Configuration;
using ReportGenerator.Core.Data.Models;
using ReportGenerator.Core.Errors;
using ReportGenerator.Core.Interfaces;
using System.Data;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace ReportGenerator.Core.Data
{
    /// <summary>
    /// פריט במטמון עם תאריך יצירה ותפוגה
    /// </summary>
    internal class CacheItem<T>
    {
        public T Value { get; }
        public DateTime CreatedAt { get; }
        public DateTime ExpiresAt { get; }
        
        public bool IsExpired => DateTime.Now > ExpiresAt;
        
        public CacheItem(T value, TimeSpan expiration)
        {
            Value = value;
            CreatedAt = DateTime.Now;
            ExpiresAt = CreatedAt.Add(expiration);
        }
    }
    
    public class DataAccess : IDataAccess
    {
        private readonly string _connectionString;
        private readonly IErrorManager _errorManager;
        
        // מטמונים לפריטים נפוצים
        private static readonly ConcurrentDictionary<string, CacheItem<ReportConfig>> _reportConfigCache = new();
        private static readonly ConcurrentDictionary<string, CacheItem<string>> _monthNameCache = new();
        private static readonly ConcurrentDictionary<string, CacheItem<string>> _periodNameCache = new();
        private static readonly ConcurrentDictionary<int, CacheItem<string>> _sugtsNameCache = new();
        private static readonly ConcurrentDictionary<int, CacheItem<string>> _ishvNameCache = new();
        private static readonly ConcurrentDictionary<string, CacheItem<Dictionary<string, string>>> _columnMappingsCache = new();
        private static readonly ConcurrentDictionary<string, CacheItem<List<ParameterInfo>>> _procedureParametersCache = new();
        
        // קבועים לזמני תפוגה
        private const int MAX_CACHE_ITEMS = 500; // מספר מקסימלי של פריטים במטמון
        private const int CACHE_MINUTES_SHORT = 5;  // לנתונים שמשתנים תכופות
        private const int CACHE_MINUTES_MEDIUM = 30;  // לנתונים שמשתנים לפעמים
        private const int CACHE_MINUTES_LONG = 120;  // לנתונים שנדירות משתנים

        /// <summary>
        /// יוצר מופע חדש של מחלקת גישה לנתונים
        /// </summary>
        /// <param name="errorManager">מנהל שגיאות</param>
        /// <param name="settings">הגדרות</param>
        public DataAccess(IErrorManager errorManager, IOptions<ReportSettings> settings)
        {
            _errorManager = errorManager ?? throw new ArgumentNullException(nameof(errorManager));
            
            var settingsValue = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
            _connectionString = settingsValue.ConnectionString ?? throw new ArgumentException("Connection string cannot be null", nameof(settings));
        }

        /// <summary>
        /// יוצר מופע חדש של מחלקת גישה לנתונים (לתאימות אחורה)
        /// </summary>
        /// <param name="connectionString">מחרוזת התחברות</param>
        public DataAccess(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            _errorManager = new ErrorManager(new DbErrorLogger());
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
                    _errorManager.LogError(
                        ErrorCode.DB_Report_NotFound,
                        ErrorSeverity.Critical,
                        $"דוח בשם {reportName} לא נמצא במערכת");
                    throw new Exception($"Report Name {reportName} not found");
                }
                
                return result;
            }
            catch (SqlException ex)
            {
                _errorManager.LogError(
                    ErrorCode.DB_Query_Failed,
                    ErrorSeverity.Critical,
                    $"שגיאת SQL בזמן שליפת הגדרות דוח {reportName}",
                    ex);
                throw new Exception($"SQL error retrieving stored procedures for report {reportName}", ex);
            }
            catch (Exception ex) when (!(ex.InnerException is SqlException) && !(ex is InvalidOperationException))
            {
                _errorManager.LogError(
                    ErrorCode.DB_Connection_Failed,
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
            // בדיקה במטמון קודם
            string cacheKey = $"Month_{mnt}";
            if (_monthNameCache.TryGetValue(cacheKey, out CacheItem<string> cacheItem) && !cacheItem.IsExpired)
            {
                return cacheItem.Value;
            }
            
            try
            {
                using var connection = new SqlConnection(_connectionString);
                var result = await connection.QuerySingleOrDefaultAsync<string>(
                    "SELECT dbo.mntname(@Mnt)",
                    new { Mnt = mnt });
                    
                if (result == null)
                {
                    _errorManager.LogWarning(
                        ErrorCode.DB_MonthName_NotFound,
                        $"לא נמצא שם עבור חודש {mnt}");
                    return $"חודש {mnt}";
                }
                
                // שמירה במטמון עם תפוגה
                _monthNameCache[cacheKey] = new CacheItem<string>(result, TimeSpan.FromMinutes(CACHE_MINUTES_LONG));
                
                // ניקוי המטמון אם יש יותר מדי פריטים
                CleanupCache(_monthNameCache, MAX_CACHE_ITEMS);
                
                return result;
            }
            catch (Exception ex)
            {
                _errorManager.LogNormalError(
                    ErrorCode.DB_Query_Failed,
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
            // בדיקה במטמון קודם
            string cacheKey = $"Period_{mnt}";
            if (_periodNameCache.TryGetValue(cacheKey, out CacheItem<string> cacheItem) && !cacheItem.IsExpired)
            {
                return cacheItem.Value;
            }
            
            try
            {
                using var connection = new SqlConnection(_connectionString);
                var result = await connection.QuerySingleOrDefaultAsync<string>(
                    "SELECT dbo.PeriodName(@Mnt)",
                    new { Mnt = mnt });
                    
                if (result == null)
                {
                    _errorManager.LogWarning(
                        ErrorCode.DB_MonthName_NotFound,
                        $"לא נמצא שם תקופה עבור חודש {mnt}");
                    return $"תקופה {mnt}";
                }
                
                // שמירה במטמון עם תפוגה
                _periodNameCache[cacheKey] = new CacheItem<string>(result, TimeSpan.FromMinutes(CACHE_MINUTES_LONG));
                
                // ניקוי המטמון אם יש יותר מדי פריטים
                CleanupCache(_periodNameCache, MAX_CACHE_ITEMS);
                
                return result;
            }
            catch (Exception ex)
            {
                _errorManager.LogNormalError(
                    ErrorCode.DB_Query_Failed,
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
                    _errorManager.LogWarning(
                        ErrorCode.DB_MoazaName_NotFound,
                        "לא נמצא שם מועצה");
                    return "מועצה לא ידועה";
                }

                return result;
            }
            catch (Exception ex)
            {
                _errorManager.LogNormalError(
                    ErrorCode.DB_Query_Failed,
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
            // בדיקה במטמון
            if (_sugtsNameCache.TryGetValue(sugts, out CacheItem<string> cacheItem) && !cacheItem.IsExpired)
            {
                return cacheItem.Value;
            }
            
            try
            {
                using var connection = new SqlConnection(_connectionString);
                var result = await connection.QuerySingleOrDefaultAsync<string>(
                    "SELECT dbo.SugtsName(@Sugts)",
                    new { Sugts = sugts });

                if (result == null)
                {
                    _errorManager.LogWarning(
                        ErrorCode.DB_SugtsName_NotFound,
                        $"לא נמצא שם עבור סוג חיוב {sugts}");
                    return $"סוג חיוב {sugts}";
                }
                
                // שמירה במטמון עם תפוגה
                _sugtsNameCache[sugts] = new CacheItem<string>(result, TimeSpan.FromMinutes(CACHE_MINUTES_MEDIUM));
                
                // ניקוי המטמון אם יש יותר מדי פריטים
                CleanupCache(_sugtsNameCache, MAX_CACHE_ITEMS);
                
                return result;
            }
            catch (Exception ex)
            {
                _errorManager.LogNormalError(
                    ErrorCode.DB_Query_Failed,
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
            // בדיקה במטמון
            if (_ishvNameCache.TryGetValue(isvkod, out CacheItem<string> cacheItem) && !cacheItem.IsExpired)
            {
                return cacheItem.Value;
            }
            
            try
            {
                using var connection = new SqlConnection(_connectionString);
                var result = await connection.QuerySingleOrDefaultAsync<string>(
                    "SELECT dbo.GetCityName(@Isvkod)",
                    new { Isvkod = isvkod });

                if (result == null)
                {
                    _errorManager.LogWarning(
                        ErrorCode.DB_IshvName_NotFound,
                        $"לא נמצא שם עבור יישוב {isvkod}");
                    return $"יישוב {isvkod}";
                }
                
                // שמירה במטמון עם תפוגה
                _ishvNameCache[isvkod] = new CacheItem<string>(result, TimeSpan.FromMinutes(CACHE_MINUTES_MEDIUM));
                
                // ניקוי המטמון אם יש יותר מדי פריטים
                CleanupCache(_ishvNameCache, MAX_CACHE_ITEMS);
                
                return result;
            }
            catch (Exception ex)
            {
                _errorManager.LogNormalError(
                    ErrorCode.DB_Query_Failed,
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
                _errorManager.LogNormalError(
                    ErrorCode.DB_Query_Failed,
                    $"שגיאה בשליפת שמות עבור קודים {codes} מטבלה {tableName}",
                    ex);
                return codes;
            }
        }

        /// <summary>
        /// מקבל את הגדרות הדוח וממטמן את התוצאה לשימוש עתידי
        /// </summary>
        public async Task<ReportConfig> GetReportConfig(string reportName)
        {
            // בדיקה במטמון
            if (_reportConfigCache.TryGetValue(reportName, out CacheItem<ReportConfig> cacheItem) && !cacheItem.IsExpired)
            {
                return cacheItem.Value;
            }
            
            try
            {
                using var connection = new SqlConnection(_connectionString);
                var result = await connection.QuerySingleOrDefaultAsync<ReportConfig>(
                    "SELECT ReportID, ReportName, StoredProcName, Title, Description " +
                    "FROM ReportsGenerator WHERE ReportName = @ReportName",
                    new { ReportName = reportName });

                if (result == null)
                {
                    _errorManager.LogError(
                        ErrorCode.DB_Report_Config_Invalid,
                        ErrorSeverity.Critical,
                        $"הגדרות דוח {reportName} לא נמצאו במערכת");
                    throw new Exception($"Report configuration for {reportName} not found");
                }

                // שמירה במטמון עם תפוגה ארוכה (תצורות דוחות לא משתנות הרבה)
                _reportConfigCache[reportName] = new CacheItem<ReportConfig>(result, TimeSpan.FromMinutes(CACHE_MINUTES_LONG));
                
                // ניקוי המטמון אם יש יותר מדי פריטים
                CleanupCache(_reportConfigCache, MAX_CACHE_ITEMS);

                return result;
            }
            catch (SqlException ex)
            {
                _errorManager.LogError(
                    ErrorCode.DB_Query_Failed,
                    ErrorSeverity.Critical,
                    $"שגיאת SQL בזמן שליפת הגדרות דוח {reportName}",
                    ex);
                throw new Exception($"SQL error retrieving report configuration for {reportName}", ex);
            }
            catch (Exception ex) when (!(ex.InnerException is SqlException) && !(ex is InvalidOperationException))
            {
                _errorManager.LogError(
                    ErrorCode.DB_Connection_Failed,
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
                _errorManager.LogNormalError(
                    ErrorCode.DB_Query_Failed,
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
            // בדיקה במטמון
            if (_columnMappingsCache.TryGetValue(procNames, out CacheItem<Dictionary<string, string>> cacheItem) && !cacheItem.IsExpired)
            {
                return cacheItem.Value;
            }
            
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
                    _errorManager.LogWarning(
                        ErrorCode.DB_ColumnMapping_NotFound,
                        $"לא נמצאו מיפויי עמודות עבור פרוצדורות: {procNames}");
                }
                
                // שמירה במטמון אם יש לפחות מיפוי אחד
                if (mappings.Count > 0)
                {
                    _columnMappingsCache[procNames] = new CacheItem<Dictionary<string, string>>(mappings, TimeSpan.FromMinutes(CACHE_MINUTES_LONG));
                    
                    // ניקוי המטמון אם יש יותר מדי פריטים
                    CleanupCache(_columnMappingsCache, MAX_CACHE_ITEMS);
                }
                
                return mappings;
            }
            catch (Exception ex)
            {
                _errorManager.LogNormalError(
                    ErrorCode.DB_Query_Failed,
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
                    _errorManager.LogError(
                        ErrorCode.DB_Query_Failed,
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
                _errorManager.LogError(
                    ErrorCode.DB_TableFunc_Execution_Failed,
                    ErrorSeverity.Error,
                    $"שגיאת SQL בהרצת פונקציה טבלאית {functionName}",
                    ex);
                throw;
            }
            catch (Exception ex)
            {
                _errorManager.LogError(
                    ErrorCode.DB_Connection_Failed,
                    ErrorSeverity.Error,
                    $"שגיאת התחברות או הרצה של פונקציה טבלאית {functionName}",
                    ex);
                throw;
            }
        }

        /// <summary>
        /// הרצת פרוצדורה מאוחסנת וקבלת התוצאות כטבלה עם תמיכה בקישינג פרמטרים
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
                        _errorManager.LogError(
                            ErrorCode.DB_StoredProc_MissingParam,
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
                _errorManager.LogError(
                    ErrorCode.DB_StoredProc_Execution_Failed,
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
                _errorManager.LogError(
                    ErrorCode.DB_Connection_Failed,
                    ErrorSeverity.Error,
                    $"שגיאת התחברות או הרצה של פרוצדורה {spName}",
                    ex);
                throw;
            }
        }
        public async Task<Dictionary<string, string>> GetDefaultColumnMappings()
        {
            try
            {
                var mappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                // שליפת כל המיפויים
                using var connection = new SqlConnection(_connectionString);
                var results = await connection.QueryAsync<ColumnMapping>(
                    @"SELECT TableName, ColumnName, HebrewAlias 
              FROM ReportsGeneratorColumns");

                foreach (var result in results)
                {
                    // שמירה כמיפוי מורכב (TableName_ColumnName)
                    string compositeKey = $"{result.TableName}_{result.ColumnName}";
                    mappings[compositeKey] = result.HebrewAlias;
                    
                    // שמירה גם של השם הפשוט עצמו
                    mappings[result.ColumnName] = result.HebrewAlias;
                }
                
                // לוג למיפויים שנטענו
                _errorManager.LogInfo(
                    ErrorCode.General_Info,
                    $"נטענו {mappings.Count} מיפויים של שמות עמודות");

                return mappings;
            }
            catch (Exception ex)
            {
                _errorManager.LogError(
                    ErrorCode.DB_Query_Failed,
                    ErrorSeverity.Error,
                    "שגיאה בטעינת מיפויי עמודות",
                    ex);

                return new Dictionary<string, string>();
            }
        }

        /// <summary>
        /// מקבל מידע על פרמטרים של פרוצדורה מאוחסנת
        /// </summary>
        public async Task<List<ParameterInfo>> GetProcedureParameters(string procName)
        {
            // בדיקה במטמון
            if (_procedureParametersCache.TryGetValue(procName, out CacheItem<List<ParameterInfo>> cacheItem) && !cacheItem.IsExpired)
            {
                return cacheItem.Value;
            }
            
            var parameters = new List<ParameterInfo>();

            try
            {
                // הסרת קידומת Schema אם קיימת
                string pureProcName = procName;
                if (procName.Contains("."))
                {
                    pureProcName = procName.Split('.').Last();
                }

                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                // שימוש בשאילתה משופרת שמחזירה מידע מדויק יותר על הפרמטרים
                var query = @"
            SELECT 
                p.name AS Name,
                t.name AS DataType,
                p.is_output AS IsOutput,
                p.has_default_value AS HasDefault,
                CONVERT(NVARCHAR(100), p.default_value) AS DefaultValue
            FROM sys.parameters p
            JOIN sys.types t ON p.system_type_id = t.system_type_id
            JOIN sys.objects o ON p.object_id = o.object_id
            WHERE o.name = @ProcName 
            ORDER BY p.parameter_id";

                var result = await connection.QueryAsync<dynamic>(query, new { ProcName = pureProcName });

                // הסרת כפילויות ע"י שימוש במילון עזר
                var uniqueParams = new Dictionary<string, ParameterInfo>();

                foreach (var param in result)
                {
                    string paramName = param.Name.ToString().TrimStart('@');

                    if (!uniqueParams.ContainsKey(paramName))
                    {
                        var paramInfo = new ParameterInfo
                        {
                            Name = paramName,
                            DataType = param.DataType.ToString(),
                            DefaultValue = param.HasDefault ? param.DefaultValue?.ToString() : null,
                            IsNullable = true  // מניחים שפרמטרים אופציונליים הם nullable
                        };

                        uniqueParams[paramName] = paramInfo;
                    }
                }

                parameters = uniqueParams.Values.ToList();
            }
            catch (Exception ex)
            {
                _errorManager.LogWarning(
                    ErrorCode.Parameters_Missing,
                    $"שגיאה בקבלת פרמטרים לפרוצדורה {procName}: {ex.Message}",
                    ex);
            }

            // אם לא מצאנו פרמטרים בדרך הרגילה, ננסה להסיק מהתנהגות הפרוצדורה
            if (parameters.Count == 0)
            {
                await InferParametersFromExecution(procName, parameters);
            }

            // שמירה במטמון אם יש לפחות פרמטר אחד
            if (parameters.Count > 0)
            {
                _procedureParametersCache[procName] = new CacheItem<List<ParameterInfo>>(parameters, TimeSpan.FromMinutes(CACHE_MINUTES_LONG));
                
                // ניקוי המטמון אם יש יותר מדי פריטים
                CleanupCache(_procedureParametersCache, MAX_CACHE_ITEMS);
            }
            
            return parameters;
        }

        // פונקצית עזר להסקת פרמטרים מניסיון הרצה
        private async Task InferParametersFromExecution(string procName, List<ParameterInfo> parameters)
        {
            try
            {
                // אם כל השאר נכשל, נוסיף פרמטרים נפוצים לפרוצדורות מסוג דוח
                if (!parameters.Any(p => p.Name.Equals("mnt", StringComparison.OrdinalIgnoreCase)))
                {
                    parameters.Add(new ParameterInfo { Name = "mnt", DataType = "int", IsNullable = false });
                }

                // פרמטרים אופציונליים נפוצים
                string[] commonOptionalParams = { "isvkod", "sugtslist", "isvme", "isvad", "sughskod" };

                foreach (var paramName in commonOptionalParams)
                {
                    if (!parameters.Any(p => p.Name.Equals(paramName, StringComparison.OrdinalIgnoreCase)))
                    {
                        parameters.Add(new ParameterInfo
                        {
                            Name = paramName,
                            DataType = "nvarchar",
                            IsNullable = true,
                            DefaultValue = null
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _errorManager.LogWarning(
                    ErrorCode.Parameters_Missing,
                    $"שגיאה בהסקת פרמטרים: {ex.Message}",
                    ex);
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
        
        /// <summary>
        /// מנקה מטמון אם הוא גדול מדי
        /// </summary>
        private void CleanupCache<TKey, TValue>(ConcurrentDictionary<TKey, CacheItem<TValue>> cache, int maxItems)
        {
            // מחיקת פריטים שפג תוקפם
            var expiredItems = cache.Where(kvp => kvp.Value.IsExpired)
                                    .Select(kvp => kvp.Key)
                                    .ToList();
                                    
            foreach (var key in expiredItems)
            {
                cache.TryRemove(key, out _);
            }
            
            // אם עדיין יותר מדי פריטים, הסר את הישנים ביותר
            if (cache.Count > maxItems)
            {
                var oldestItems = cache.OrderBy(kvp => kvp.Value.CreatedAt)
                                      .Take(cache.Count - maxItems / 2)  // מסירים חצי מהמטמון
                                      .Select(kvp => kvp.Key)
                                      .ToList();
                                      
                foreach (var key in oldestItems)
                {
                    cache.TryRemove(key, out _);
                }
            }
        }
    }
}
