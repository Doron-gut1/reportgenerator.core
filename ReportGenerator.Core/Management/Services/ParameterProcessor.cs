using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using ReportGenerator.Core.Data.Models;
using ReportGenerator.Core.Errors;
using ReportGenerator.Core.Interfaces;

namespace ReportGenerator.Core.Management.Services
{
    /// <summary>
    /// מחלקה לעיבוד פרמטרים לדוחות
    /// </summary>
    internal class ParameterProcessor
    {
        private readonly IDataAccess _dataAccess;
        private readonly IErrorManager _errorManager;

        /// <summary>
        /// יוצר מעבד פרמטרים חדש
        /// </summary>
        public ParameterProcessor(IDataAccess dataAccess, IErrorManager errorManager)
        {
            _dataAccess = dataAccess ?? throw new ArgumentNullException(nameof(dataAccess));
            _errorManager = errorManager ?? throw new ArgumentNullException(nameof(errorManager));
        }

        /// <summary>
        /// ממיר מערך פרמטרים למילון שימושי ומוסיף פרמטרים מיוחדים
        /// </summary>
        public async Task<Dictionary<string, ParamValue>> ProcessParameters(string reportName, string procName, object[] paramArray)
        {
            // שלב 1: המרת מערך פרמטרים למילון
            var parameters = await ParseParameters(reportName, procName, paramArray);
            
            // שלב 2: הוספת פרמטרים חסרים
            await FillMissingParameters(procName, parameters);
            
            // שלב 3: עיבוד פרמטרים מיוחדים והוספת פרמטרים נגזרים
            parameters = await ProcessSpecialParameters(parameters);
            
            return parameters;
        }

        /// <summary>
        /// ממיר מערך פרמטרים למילון שימושי
        /// </summary>
        private async Task<Dictionary<string, ParamValue>> ParseParameters(string reportName, string procName, object[] paramArray)
        {
            var result = new Dictionary<string, ParamValue>(StringComparer.OrdinalIgnoreCase);

            try
            {
                // פירוש הפרמטרים שהועברו
                if (paramArray != null && paramArray.Length > 0)
                {
                    for (int i = 0; i < paramArray.Length; i += 3)
                    {
                        if (i + 2 >= paramArray.Length)
                        {
                            _errorManager.LogError(
                                ErrorCode.Parameters_Invalid,
                                ErrorSeverity.Error,
                                $"מערך הפרמטרים אינו בפורמט הנכון");
                            throw new ArgumentException("Parameter array is not in the correct format");
                        }

                        // בדיקת שם פרמטר
                        string paramName = paramArray[i]?.ToString();
                        if (string.IsNullOrEmpty(paramName))
                        {
                            _errorManager.LogError(
                                ErrorCode.Parameters_Invalid,
                                ErrorSeverity.Error,
                                $"שם פרמטר במיקום {i} הוא null או ריק");
                            throw new ArgumentException($"Parameter name at position {i} is null or empty");
                        }

                        // הערך יכול להיות null - זה תקין
                        object paramValue = paramArray[i + 1];

                        // בדיקת סוג הפרמטר
                        DbType paramType;
                        object dbTypeObject = paramArray[i + 2];

                        if (dbTypeObject is DbType dbTypeEnum)
                        {
                            // אם זה כבר DbType, השתמש בו ישירות
                            paramType = dbTypeEnum;
                        }
                        else
                        {
                            // אחרת, נסה להמיר למספר ואז ל-DbType
                            try
                            {
                                int dbTypeValue = Convert.ToInt32(dbTypeObject);
                                paramType = (DbType)dbTypeValue;
                            }
                            catch (Exception ex)
                            {
                                _errorManager.LogError(
                                    ErrorCode.Parameters_Type_Mismatch,
                                    ErrorSeverity.Error,
                                    $"סוג הפרמטר במיקום {i + 2} אינו DbType תקין: {dbTypeObject}",
                                    ex);
                                throw new ArgumentException($"Parameter type at position {i + 2} is not a valid DbType: {dbTypeObject}", ex);
                            }
                        }

                        // הוספת הפרמטר למילון התוצאה
                        result.Add(paramName, new ParamValue(paramValue, paramType));
                    }
                }

                return result;
            }
            catch (Exception ex) when (!(ex is ArgumentException))
            {
                _errorManager.LogError(
                    ErrorCode.Parameters_Invalid,
                    ErrorSeverity.Error,
                    "שגיאה בניתוח מערך הפרמטרים",
                    ex);
                throw new ArgumentException("Error parsing parameters array", ex);
            }
        }

        /// <summary>
        /// מוסיף פרמטרים חסרים לרשימת הפרמטרים
        /// </summary>
        private async Task FillMissingParameters(string procName, Dictionary<string, ParamValue> parameters)
        {
            try
            {
                // קבלת רשימת הפרמטרים של הפרוצדורה
                var procParams = await _dataAccess.GetProcedureParameters(procName);

                // הוספת פרמטרים חסרים
                int addedCount = 0;
                foreach (var param in procParams)
                {
                    // בדיקה אם הפרמטר כבר קיים (תוך התעלמות מרישיות)
                    bool exists = parameters.Keys.Any(k =>
                        string.Equals(k, param.Name, StringComparison.OrdinalIgnoreCase));

                    if (!exists)
                    {
                        // יצירת ערך ברירת מחדל אם צריך
                        object defaultValue = null;

                        // אם מדובר במחרוזת, ערך ברירת המחדל יהיה null (מה שמתאים לפרמטרים אופציונליים)
                        if (param.DataType.Contains("varchar") || param.DataType.Contains("char"))
                        {
                            defaultValue = null;
                        }
                        else
                        {
                            // עבור שאר הטיפוסים, נשתמש במתודה הקיימת
                            defaultValue = GetDefaultValueForType(param.DataType);

                            // אם פרמטר מוגדר כ-nullable, אפשר לשלוח null במקום 0
                            if (param.IsNullable &&
                                (param.DataType.Contains("int") || param.DataType.Contains("bit")))
                            {
                                defaultValue = null;
                            }
                        }

                        // בחירת DbType המתאים
                        DbType dbType = GetDbTypeForSqlType(param.DataType);

                        // הוספת הפרמטר
                        parameters.Add(param.Name, new ParamValue(defaultValue, dbType));
                        addedCount++;
                    }
                }
            }
            catch (Exception ex)
            {
                _errorManager.LogWarning(
                    ErrorCode.Parameters_Missing,
                    $"לא ניתן להוסיף פרמטרים חסרים לפרוצדורה {procName}: {ex.Message}",
                    ex);
            }
        }

        /// <summary>
        /// מטפל בפרמטרים מיוחדים ומחשב פרמטרים נגזרים כגון שמות
        /// </summary>
        private async Task<Dictionary<string, ParamValue>> ProcessSpecialParameters(Dictionary<string, ParamValue> parameters)
        {
            var enhancedParams = new Dictionary<string, ParamValue>(parameters, StringComparer.OrdinalIgnoreCase);

            try
            {
                await ProcessMonthParameter(enhancedParams);
                await ProcessChargeTypeParameter(enhancedParams);
                await ProcessSettlementParameter(enhancedParams);
                await ProcessMoazaParameter(enhancedParams);

                return enhancedParams;
            }
            catch (Exception ex)
            {
                _errorManager.LogNormalError(
                    ErrorCode.Parameters_Invalid,
                    "שגיאה בעיבוד פרמטרים מיוחדים",
                    ex);

                return parameters;
            }
        }

        private async Task ProcessMoazaParameter(Dictionary<string, ParamValue> parameters)
        {
            try
            {
                string moazaName = await _dataAccess.GetMoazaName();
                parameters.Add("rashutName", new ParamValue(moazaName, DbType.String));
            }
            catch (Exception ex)
            {
                _errorManager.LogWarning(
                    ErrorCode.Parameters_Invalid,
                    $"שגיאה בעיבוד פרמטר מועצה: {ex.Message}");
            }
        }

        private async Task ProcessMonthParameter(Dictionary<string, ParamValue> parameters)
        {
            if (parameters.TryGetValue("mnt", out ParamValue mntParam) && mntParam.Value != null)
            {
                try
                {
                    int mntValue = Convert.ToInt32(mntParam.Value);
                    string monthName = await _dataAccess.GetMonthName(mntValue);
                    string periodName = await _dataAccess.GetPeriodName(mntValue);

                    if (!parameters.ContainsKey("mntname"))
                    {
                        parameters.Add("mntname", new ParamValue(monthName, DbType.String));
                    }

                    if (!parameters.ContainsKey("PeriodName"))
                    {
                        parameters.Add("PeriodName", new ParamValue(periodName, DbType.String));
                    }
                }
                catch (Exception ex)
                {
                    _errorManager.LogWarning(
                        ErrorCode.Parameters_Invalid,
                        $"שגיאה בעיבוד פרמטר חודש (mnt): {ex.Message}");
                }
            }
        }

        private async Task ProcessChargeTypeParameter(Dictionary<string, ParamValue> parameters)
        {
            if (parameters.TryGetValue("sugts", out ParamValue sugtsParam) && sugtsParam.Value != null)
            {
                try
                {
                    int sugtsValue = Convert.ToInt32(sugtsParam.Value);
                    string sugtsName = await _dataAccess.GetSugtsName(sugtsValue);

                    if (!parameters.ContainsKey("sugtsname"))
                    {
                        parameters.Add("sugtsname", new ParamValue(sugtsName, DbType.String));
                    }
                }
                catch (Exception ex)
                {
                    _errorManager.LogWarning(
                        ErrorCode.Parameters_Invalid,
                        $"שגיאה בעיבוד פרמטר סוג חיוב (sugts): {ex.Message}");
                }
            }
            else if (parameters.TryGetValue("sugtslist", out ParamValue sugtsListParam) && sugtsListParam.Value != null)
            {
                try
                {
                    string sugtsListValue = sugtsListParam.Value.ToString();

                    if (!string.IsNullOrEmpty(sugtsListValue) && !sugtsListValue.Contains(","))
                    {
                        if (int.TryParse(sugtsListValue, out int singleSugtsValue))
                        {
                            string sugtsName = await _dataAccess.GetSugtsName(singleSugtsValue);

                            if (!parameters.ContainsKey("sugtsname"))
                            {
                                parameters.Add("sugtsname", new ParamValue(sugtsName, DbType.String));
                            }
                        }
                    }
                    else
                    {
                        if (!parameters.ContainsKey("sugtsname"))
                        {
                            parameters.Add("sugtsname", new ParamValue("מספר סוגי חיוב", DbType.String));
                        }
                    }
                }
                catch (Exception ex)
                {
                    _errorManager.LogWarning(
                        ErrorCode.Parameters_Invalid,
                        $"שגיאה בעיבוד פרמטר רשימת סוגי חיוב (sugtslist): {ex.Message}");
                }
            }
            else
            {
                if (!parameters.ContainsKey("sugtsname"))
                {
                    parameters.Add("sugtsname", new ParamValue("כל סוגי חיוב", DbType.String));
                }
            }
        }

        private async Task ProcessSettlementParameter(Dictionary<string, ParamValue> parameters)
        {
            if (parameters.TryGetValue("isvkod", out ParamValue isvkodParam) && isvkodParam.Value != null)
            {
                try
                {
                    string isvkodValue = isvkodParam.Value.ToString();

                    if (!string.IsNullOrEmpty(isvkodValue) && !isvkodValue.Contains(","))
                    {
                        if (int.TryParse(isvkodValue, out int singleIsvkodValue))
                        {
                            string isvName = await _dataAccess.GetIshvName(singleIsvkodValue);

                            if (!parameters.ContainsKey("ishvname"))
                            {
                                parameters.Add("ishvname", new ParamValue(isvName, DbType.String));
                            }
                        }
                    }
                    else
                    {
                        if (!parameters.ContainsKey("ishvname"))
                        {
                            parameters.Add("ishvname", new ParamValue("מספר יישובים", DbType.String));
                        }
                    }
                }
                catch (Exception ex)
                {
                    _errorManager.LogWarning(
                        ErrorCode.Parameters_Invalid,
                        $"שגיאה בעיבוד פרמטר יישוב (isvkod): {ex.Message}");
                }
            }
            else
            {
                if (!parameters.ContainsKey("ishvname"))
                {
                    parameters.Add("ishvname", new ParamValue("כל היישובים", DbType.String));
                }
            }
        }

        /// <summary>
        /// פונקצית עזר לקביעת DbType המתאים
        /// </summary>
        private DbType GetDbTypeForSqlType(string sqlType)
        {
            sqlType = sqlType.ToLowerInvariant();

            if (sqlType.Contains("int"))
                return DbType.Int32;
            if (sqlType.Contains("bigint"))
                return DbType.Int64;
            if (sqlType.Contains("bit"))
                return DbType.Boolean;
            if (sqlType.Contains("decimal") || sqlType.Contains("numeric") || sqlType.Contains("money"))
                return DbType.Decimal;
            if (sqlType.Contains("float") || sqlType.Contains("real"))
                return DbType.Double;
            if (sqlType.Contains("date") || sqlType.Contains("time"))
                return DbType.DateTime;
            if (sqlType.Contains("nvarchar") || sqlType.Contains("nchar") || sqlType.Contains("ntext"))
                return DbType.String;
            if (sqlType.Contains("varchar") || sqlType.Contains("char") || sqlType.Contains("text"))
                return DbType.AnsiString;

            // ברירת מחדל
            return DbType.String;
        }

        /// <summary>
        /// מחזיר ערך ברירת מחדל לפי סוג נתונים
        /// </summary>
        private object GetDefaultValueForType(string dataType)
        {
            switch (dataType.ToLower())
            {
                case "int":
                case "smallint":
                case "tinyint":
                case "bigint":
                    return 0;

                case "bit":
                    return false;

                case "decimal":
                case "numeric":
                case "money":
                case "smallmoney":
                case "float":
                case "real":
                    return 0.0;

                case "datetime":
                case "date":
                case "datetime2":
                case "smalldatetime":
                    return null; // או DateTime.Now אם מעדיפים ערך לא-null

                case "nvarchar":
                case "varchar":
                case "char":
                case "nchar":
                case "text":
                case "ntext":
                    return ""; // מחרוזת ריקה

                default:
                    return null;
            }
        }
    }
}