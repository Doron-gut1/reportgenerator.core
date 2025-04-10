using System;
using System.Threading.Tasks;
using System.Data;
using System.Collections.Generic;
using ReportGenerator.Core.Data;
using ReportGenerator.Core.Data.Models;
using ReportGenerator.Core.Generators;
using ReportGenerator.Core.Management.Enums;
using ReportGenerator.Core.Errors;

namespace ReportGenerator.Core.Management
{
    /// <summary>
    /// מנהל הדוחות הראשי - מקשר בין כל רכיבי המערכת
    /// </summary>
    public class ReportManager
    {
        private readonly DataAccess _dataAccess;
        private readonly HtmlTemplateManager _templateManager;
        private HtmlTemplateProcessor _templateProcessor;
        private readonly HtmlBasedPdfGenerator _htmlPdfGenerator;
        private ExcelGenerator _excelGenerator;

        /// <summary>
        /// יוצר מופע חדש של מנהל הדוחות
        /// </summary>
        /// <param name="connectionString">מחרוזת התחברות לבסיס הנתונים</param>
        /// <param name="templatesFolder">נתיב לתיקיית תבניות HTML</param>
        /// <param name="logsFolder">נתיב לתיקיית קבצי לוג</param>
        /// <param name="chromePath">נתיב לקובץ ההפעלה של Chrome (אופציונלי)</param>
        public ReportManager(string connectionString, string templatesFolder, string logsFolder = null, string chromePath = null)
        {
            try
            {
                // אתחול מערכת רישום השגיאות
                DbErrorLogger.Initialize(connectionString, logsFolder);
                
                // אתחול גישה לנתונים
                _dataAccess = new DataAccess(connectionString);
                
                // הגדרת רכיבי מערכת התבניות החדשה
                _templateManager = new HtmlTemplateManager(templatesFolder);
                
                // יצירת מופע ראשוני של מעבד התבניות עם מילון ריק
                _templateProcessor = new HtmlTemplateProcessor(new Dictionary<string, string>());
                
                // יצירת ממיר HTML ל-PDF
                var pdfConverter = new PuppeteerHtmlToPdfConverter(chromePath);
                
                // יוצר ה-PDF מבוסס HTML
                _htmlPdfGenerator = new HtmlBasedPdfGenerator(_templateManager, _templateProcessor, pdfConverter);
                
                // יוצר אקסל ללא מיפויי כותרות בשלב זה (יוגדרו מאוחר יותר)
                _excelGenerator = new ExcelGenerator();
                
                ErrorManager.LogInfo(
                    "ReportManager_Initialized",
                    $"מנהל הדוחות אותחל בהצלחה. תיקיית תבניות: {templatesFolder}");
            }
            catch (Exception ex)
            {
                // במקרה של שגיאת אתחול, זו שגיאה קריטית
                ErrorManager.LogCriticalError(
                    ErrorCodes.Report.Parameters_Invalid,
                    "שגיאה באתחול מנהל הדוחות",
                    ex);
                throw new Exception("שגיאה באתחול מנהל הדוחות", ex);
            }
        }
        public void GenerateReportAsync(string reportName, OutputFormat format, params object[] parameters)
        {
            // הפעלת התהליך בחוט נפרד
            Task.Run(async () =>
            {
                try
                {
                    // הפקת הדוח באמצעות המתודה הקיימת - שים לב לawait
                    byte[] reportData = await Task.Run(() => GenerateReport(reportName, format, parameters));

                    // כל השאר נשאר אותו דבר
                    string DEFAULT_WORKING_DIRECTORY = "C:\\Epr";
                    string TEMP_DATA_FOLDER = "Temp";
                    string localDrive = Path.Combine(DEFAULT_WORKING_DIRECTORY, TEMP_DATA_FOLDER);
                    //string targetFilePath = Path.Combine(@"\\tsclient\", localDrive.Replace(":", ""));
                    string targetFilePath = localDrive;

                    // וידוא שהתיקיות קיימות
                    Directory.CreateDirectory(localDrive);
                    Directory.CreateDirectory(targetFilePath);

                    // קביעת סיומת הקובץ
                    string fileExt = format == OutputFormat.PDF ? "pdf" : "xlsx";

                    // יצירת שם קובץ ייחודי
                    string fileName = $"{reportName}_{DateTime.Now:yyyyMMdd_HHmmss}.{fileExt}";
                    string fullPath = Path.Combine(targetFilePath, fileName);

                    // שמירת הקובץ
                    File.WriteAllBytes(fullPath, reportData);

                    // יצירת קובץ הדיאלוג
                    string listenerDialogFile = Path.Combine(targetFilePath,
                        $"{Path.GetFileNameWithoutExtension(fileName)}.opdialog");
                    File.Create(listenerDialogFile).Close();

                    ErrorManager.LogInfo(
                        "Report_Saved_Successfully",
                        $"הדוח {reportName} נשמר בהצלחה בנתיב {fullPath}",
                        reportName: reportName);
                }
                catch (Exception ex)
                {
                    ErrorManager.LogCriticalError(
                        ErrorCodes.Report.Generation_Failed,
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
            ErrorManager.ClearErrors();

            // רישום תחילת הפקת הדוח
            ErrorManager.LogInfo(
                "Report_Generation_Started",
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

                if (format == OutputFormat.PDF)               
                    _templateProcessor = new HtmlTemplateProcessor(columnMappings);   // עדכון מעבד התבניות עם המיפויים
                else          
                    _excelGenerator = new ExcelGenerator(columnMappings); // עדכון מחלקת האקסל עם המיפויים
                
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
                        ErrorManager.LogCriticalError(
                            ErrorCodes.Template.Not_Found,
                            $"לא נמצאה תבנית HTML עבור דוח {reportName}. יש ליצור קובץ תבנית בשם '{reportName}.html'",
                            reportName: reportName);
                            
                        throw new Exception($"No HTML template found for report {reportName}. Please create an HTML template file named '{reportName}.html'");
                    }
                    
                    // שימוש בגישה החדשה מבוססת HTML
                    result = await _htmlPdfGenerator.GenerateFromTemplate(
                        reportName, reportConfig.Title, dataTables, parsedParams);
                }
                else // Excel
                {
                    // יצירת קובץ אקסל עם כל הנתונים
                    result = _excelGenerator.Generate(dataTables, reportConfig.Title);
                }
                
                var duration = DateTime.Now - startTime;
                
                // רישום סיום מוצלח של הפקת הדוח
                ErrorManager.LogInfo(
                    "Report_Generation_Completed",
                    $"הפקת דוח {reportName} הסתיימה בהצלחה בפורמט {format}. משך: {duration.TotalSeconds:F2} שניות. גודל: {result.Length / 1024:N0} KB",
                    reportName: reportName);
                    
                return result;
            }
            catch (Exception ex)
            {
                // במקרה של שגיאה שלא טופלה בקוד הקודם, רשום אותה ופרטים נוספים
                ErrorManager.LogCriticalError(
                    ErrorCodes.Report.Generation_Failed,
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
                            ErrorManager.LogError(
                                ErrorCodes.Report.Parameters_Invalid,
                                ErrorSeverity.Error,
                                $"מערך הפרמטרים אינו בפורמט הנכון");
                            throw new ArgumentException("Parameter array is not in the correct format");
                        }

                        // בדיקת שם פרמטר
                        string paramName = paramArray[i]?.ToString();
                        if (string.IsNullOrEmpty(paramName))
                        {
                            ErrorManager.LogError(
                                ErrorCodes.Report.Parameters_Invalid,
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
                                ErrorManager.LogError(
                                    ErrorCodes.Report.Parameters_Type_Mismatch,
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
                ErrorManager.LogError(
                    ErrorCodes.Report.Parameters_Invalid,
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
                foreach (var param in procParams)
                {
                    string paramName = param.Name;
                    if (paramName.StartsWith("@"))
                        paramName = paramName.Substring(1); // הסרת @ מתחילת השם

                    // בדיקה אם הפרמטר כבר קיים
                    if (!parameters.ContainsKey(paramName))
                    {
                        // יצירת ערך ברירת מחדל מתאים
                        object defaultValue = GetDefaultValueForType(param.DataType);

                        ErrorManager.LogInfo(
                            "Parameter_Auto_Added",
                            $"פרמטר {paramName} התווסף אוטומטית לפרוצדורה {procName} עם ערך ברירת מחדל");

                        // הוספת הפרמטר עם ערך ברירת מחדל
                        parameters.Add(paramName, new ParamValue(defaultValue, param.GetDbType()));
                    }
                }
            }
            catch (Exception ex)
            {
                // רק רושמים שגיאה, אבל לא זורקים חריגה כדי לא לשבור את כל התהליך
                ErrorManager.LogWarning(
                    ErrorCodes.Report.Parameters_Missing,
                    $"לא ניתן להוסיף פרמטרים חסרים לפרוצדורה {procName}: {ex.Message}",
                    ex);
            }
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
                ErrorManager.LogNormalError(
                    ErrorCodes.Report.Parameters_Invalid,
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
                ErrorManager.LogWarning(
                    ErrorCodes.Report.Parameters_Invalid,
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
                    ErrorManager.LogWarning(
                        ErrorCodes.Report.Parameters_Invalid,
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
                    ErrorManager.LogWarning(
                        ErrorCodes.Report.Parameters_Invalid,
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
                    ErrorManager.LogWarning(
                        ErrorCodes.Report.Parameters_Invalid,
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
                    ErrorManager.LogWarning(
                        ErrorCodes.Report.Parameters_Invalid,
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
