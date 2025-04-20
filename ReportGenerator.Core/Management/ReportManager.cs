using System;
using System.Threading.Tasks;
using System.Data;
using System.Collections.Generic;
using Microsoft.Extensions.Options;
using ReportGenerator.Core.Configuration;
using ReportGenerator.Core.Data;
using ReportGenerator.Core.Data.Models;
using ReportGenerator.Core.Errors;
using ReportGenerator.Core.Interfaces;
using ReportGenerator.Core.Management.Enums;

namespace ReportGenerator.Core.Management
{
    /// <summary>
    /// מנהל הדוחות הראשי - מקשר בין כל רכיבי המערכת
    /// </summary>
    public class ReportManager : IReportGenerator
    {
        private readonly IDataAccess _dataAccess;
        private readonly ITemplateManager _templateManager;
        private readonly ITemplateProcessor _templateProcessor;
        private readonly IPdfGenerator _pdfGenerator;
        private readonly IExcelGenerator _excelGenerator;
        private readonly IErrorManager _errorManager;
        private readonly ReportSettings _settings;

        /// <summary>
        /// יוצר מופע חדש של מנהל הדוחות
        /// </summary>
        public ReportManager(
            IDataAccess dataAccess,
            ITemplateManager templateManager,
            ITemplateProcessor templateProcessor,
            IPdfGenerator pdfGenerator,
            IExcelGenerator excelGenerator,
            IErrorManager errorManager,
            IOptions<ReportSettings> settings)
        {
            _dataAccess = dataAccess ?? throw new ArgumentNullException(nameof(dataAccess));
            _templateManager = templateManager ?? throw new ArgumentNullException(nameof(templateManager));
            _templateProcessor = templateProcessor ?? throw new ArgumentNullException(nameof(templateProcessor));
            _pdfGenerator = pdfGenerator ?? throw new ArgumentNullException(nameof(pdfGenerator));
            _excelGenerator = excelGenerator ?? throw new ArgumentNullException(nameof(excelGenerator));
            _errorManager = errorManager ?? throw new ArgumentNullException(nameof(errorManager));
            _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
        }

        /// <summary>
        /// מייצר דוח בצורה אסינכרונית ושומר אותו לקובץ
        /// </summary>
        public void GenerateReportAsync(string reportName, OutputFormat format, params object[] parameters)
        {
            // הפעלת התהליך בחוט נפרד
            Task.Run(async () =>
            {
                try
                {
                    // הפקת הדוח באמצעות המתודה הקיימת - שים לב לawait
                    byte[] reportData = await Task.Run(() => GenerateReport(reportName, format, parameters));

                    // שימוש בהגדרות מקובץ קונפיגורציה
                    var outputFolder = _settings.OutputFolder;
                    //if (string.IsNullOrEmpty(outputFolder))
                    //{
                    //    outputFolder = Path.Combine(_settings.TempFolder);
                    //}

                    // וידוא שהתיקיות קיימות
                    Directory.CreateDirectory(outputFolder);

                    // קביעת סיומת הקובץ
                    string fileExt = format == OutputFormat.PDF ? "pdf" : "xlsx";

                    // יצירת שם קובץ ייחודי
                    string fileName = $"{reportName}_{DateTime.Now:yyyyMMdd_HHmmss}.{fileExt}";
                    string fullPath = Path.Combine(outputFolder, fileName);

                    // שמירת הקובץ
                    File.WriteAllBytes(fullPath, reportData);

                    // יצירת קובץ הדיאלוג (אם נדרש)
                    string listenerDialogFile = Path.Combine(outputFolder, $"{Path.GetFileNameWithoutExtension(fileName)}.opdialog");
                    File.Create(listenerDialogFile).Close();

                    _errorManager.LogInfo(
                        ErrorCode.General_Error,  // שימוש ב-enum
                        $"הדוח {reportName} נשמר בהצלחה בנתיב {fullPath}",
                        reportName: reportName);
                }
                catch (Exception ex)
                {
                    _errorManager.LogCriticalError(
                        ErrorCode.Report_Generation_Failed,  // שימוש ב-enum
                        $"שגיאה בהפקת ושמירת דוח {reportName}",
                        ex,
                        reportName: reportName);
                }
            });
        }

        /// <summary>
        /// מייצר דוח לפי שם, פורמט ופרמטרים
        /// </summary>
        /// <param name="reportName">שם הדוח</param>
        /// <param name="format">פורמט הפלט (PDF/Excel)</param>
        /// <param name="parameters">פרמטרים לדוח</param>
        /// <returns>מערך בייטים של הקובץ המבוקש</returns>
        public async Task<byte[]> GenerateReport(string reportName, OutputFormat format, params object[] parameters)
        {
            // ניקוי שגיאות מהפקות קודמות
            _errorManager.ClearErrors();

            // רישום תחילת הפקת הדוח
            _errorManager.LogInfo(
                ErrorCode.General_Error,  // שימוש ב-enum
                $"התחלת הפקת דוח {reportName} בפורמט {format}");

            // מעקב אחר משך זמן ההפקה
            var startTime = DateTime.Now;

            try
            {
                // קבלת הגדרות הדוח
                var reportConfig = await _dataAccess.GetReportConfig(reportName);

                // שינוי כאן: קודם נקבל את שם הפרוצדורה ואז נפרסר פרמטרים
                string procName = reportConfig.StoredProcName.Split(';')[0].Trim(); // מקבלים את הפרוצדורה הראשונה

                // המרת פרמטרים למילון כולל הוספת פרמטרים חסרים
                var parsedParams = await ParseParameters(reportName, procName, parameters);

                // קבלת מיפויי שמות עמודות לעברית
                var columnMappings = await _dataAccess.GetColumnMappings(reportConfig.StoredProcName);

                parsedParams = await ProcessSpecialParameters(parsedParams);

                // הרצת כל הפרוצדורות
                var dataTables = await _dataAccess.ExecuteMultipleStoredProcedures(reportConfig.StoredProcName, parsedParams);

                // יצירת הדוח בפורמט המבוקש
                byte[] result;
                if (format == OutputFormat.PDF)
                {
                    // וידוא שתבנית HTML קיימת
                    if (!_templateManager.TemplateExists(reportName))
                    {
                        _errorManager.LogCriticalError(
                            ErrorCode.Template_Not_Found,  // שימוש ב-enum
                            $"לא נמצאה תבנית HTML עבור דוח {reportName}. יש ליצור קובץ תבנית בשם '{reportName}.html'",
                            reportName: reportName);

                        throw new Exception($"No HTML template found for report {reportName}. Please create an HTML template file named '{reportName}.html'");
                    }

                    // שימוש בגישה החדשה מבוססת HTML
                    result = await _pdfGenerator.GenerateFromTemplate(
                        reportName, reportConfig.Title, dataTables, parsedParams);
                }
                else // Excel
                {
                    // יצירת קובץ אקסל עם כל הנתונים
                    result = _excelGenerator.Generate(dataTables, reportConfig.Title);
                }

                var duration = DateTime.Now - startTime;

                // רישום סיום מוצלח של הפקת הדוח
                _errorManager.LogInfo(
                    ErrorCode.General_Error,  // שימוש ב-enum
                    $"הפקת דוח {reportName} הסתיימה בהצלחה בפורמט {format}. משך: {duration.TotalSeconds:F2} שניות. גודל: {result.Length / 1024:N0} KB",
                    reportName: reportName);

                return result;
            }
            catch (Exception ex)
            {
                // במקרה של שגיאה שלא טופלה בקוד הקודם, רשום אותה ופרטים נוספים
                _errorManager.LogCriticalError(
                    ErrorCode.Report_Generation_Failed,  // שימוש ב-enum
                    $"שגיאה בהפקת דוח {reportName}",
                    ex,
                    reportName: reportName);

                throw new Exception($"Error generating report {reportName}: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// המרת מערך פרמטרים לפורמט המובן למערכת
        /// </summary>
        /// <summary>
        /// המרת מערך פרמטרים לפורמט המובן למערכת ומוסיף פרמטרים חסרים
        /// </summary>
        private async Task<Dictionary<string, ParamValue>> ParseParameters(string reportName, string procName, object[] paramArray)
        {
            var result = new Dictionary<string, ParamValue>(StringComparer.OrdinalIgnoreCase);

            try
            {
                // 1. פירוש הפרמטרים שהועברו
                if (paramArray != null && paramArray.Length > 0)
                {
                    for (int i = 0; i < paramArray.Length; i += 3)
                    {
                        if (i + 2 >= paramArray.Length)
                        {
                            _errorManager.LogError(
                                ErrorCode.Parameters_Invalid,  // שימוש ב-enum
                                ErrorSeverity.Error,
                                $"מערך הפרמטרים אינו בפורמט הנכון");
                            throw new ArgumentException("Parameter array is not in the correct format");
                        }

                        // בדיקת שם פרמטר
                        string paramName = paramArray[i]?.ToString();
                        if (string.IsNullOrEmpty(paramName))
                        {
                            _errorManager.LogError(
                                ErrorCode.Parameters_Invalid,  // שימוש ב-enum
                                ErrorSeverity.Error,
                                $"שם פרמטר במיקום {i} הוא null או ריק");
                            throw new ArgumentException($"Parameter name at position {i} is null or empty");
                        }

                        // הערך יכול להיות null - זה תקין
                        object paramValue = paramArray[i + 1];

                        // בדיקת סוג הפרמטר - יכול להיות enum של DbType או int
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
                                    ErrorCode.Parameters_Type_Mismatch,  // שימוש ב-enum
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

                // 2. קבלת רשימת הפרמטרים של הפרוצדורה ומילוי הפרמטרים החסרים
                await FillMissingParameters(procName, result);

                return result;
            }
            catch (Exception ex) when (!(ex is ArgumentException))
            {
                _errorManager.LogError(
                    ErrorCode.Parameters_Invalid,  // שימוש ב-enum
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
                    ErrorCode.Parameters_Missing,  // שימוש ב-enum
                    $"לא ניתן להוסיף פרמטרים חסרים לפרוצדורה {procName}: {ex.Message}",
                    ex);
            }
        }

        // פונקצית עזר חדשה לקביעת DbType המתאים
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

        /// <summary>
        /// מטפל בפרמטרים מיוחדים ומחשב פרמטרים נגזרים כגון שמות
        /// </summary>
        /// <param name="parameters">מילון הפרמטרים המקורי</param>
        /// <returns>מילון מעודכן עם פרמטרים נוספים</returns>
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
                    ErrorCode.Parameters_Invalid,  // שימוש ב-enum
                    "Error processing special parameters",
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
                    ErrorCode.Parameters_Invalid,  // שימוש ב-enum
                    $"Error processing moaza parameter: {ex.Message}");
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
                        ErrorCode.Parameters_Invalid,  // שימוש ב-enum
                        $"Error processing month parameter (mnt): {ex.Message}");
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
                        ErrorCode.Parameters_Invalid,  // שימוש ב-enum
                        $"Error processing charge type parameter (sugts): {ex.Message}");
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
                        ErrorCode.Parameters_Invalid,  // שימוש ב-enum
                        $"Error processing charge type list parameter (sugtslist): {ex.Message}");
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
                        ErrorCode.Parameters_Invalid,  // שימוש ב-enum
                        $"Error processing settlement parameter (isvkod): {ex.Message}");
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
    }
}
