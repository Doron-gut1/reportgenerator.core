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
                // המרת פרמטרים למילון
                var parsedParams = ParseParameters(parameters);
                
                // קבלת הגדרות הדוח
                var reportConfig = await _dataAccess.GetReportConfig(reportName);
                
                // קבלת מיפויי שמות עמודות לעברית
                var columnMappings = await _dataAccess.GetColumnMappings(reportConfig.StoredProcName);
                
                if(format == OutputFormat.PDF)               
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
        private Dictionary<string, ParamValue> ParseParameters(object[] paramArray)
        {
            var result = new Dictionary<string, ParamValue>(StringComparer.OrdinalIgnoreCase);
            
            try
            {
                if (paramArray == null || paramArray.Length == 0)
                {
                    ErrorManager.LogWarning(
                        ErrorCodes.Report.Parameters_Missing,
                        "לא הועברו פרמטרים להפקת הדוח");
                    return result;
                }
                
                for (int i = 0; i < paramArray.Length; i += 3)
                {
                    if (i + 2 >= paramArray.Length)
                    {
                        ErrorManager.LogError(
                            ErrorCodes.Report.Parameters_Invalid,
                            ErrorSeverity.Error,
                            "מערך הפרמטרים אינו בפורמט הנכון");
                            
                        throw new ArgumentException("Parameter array is not in the correct format");
                    }

                    string paramName = paramArray[i]?.ToString();
                    if (string.IsNullOrEmpty(paramName))
                    {
                        ErrorManager.LogError(
                            ErrorCodes.Report.Parameters_Invalid,
                            ErrorSeverity.Error,
                            $"שם פרמטר במיקום {i} הוא null או ריק");
                            
                        throw new ArgumentException($"Parameter name at position {i} is null or empty");
                    }

                    object paramValue = paramArray[i + 1];
                    
                    if (!(paramArray[i + 2] is int dbTypeValue))
                    {
                        ErrorManager.LogError(
                            ErrorCodes.Report.Parameters_Type_Mismatch,
                            ErrorSeverity.Error,
                            $"סוג הפרמטר במיקום {i + 2} אינו DbType");
                            
                        throw new ArgumentException($"Parameter type at position {i + 2} is not a valid DbType");
                    }
                    
                    DbType paramType = (DbType)dbTypeValue;

                    result.Add(paramName, new ParamValue(paramValue, paramType));
                }
                
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
        /// מטפל בפרמטרים מיוחדים ומחשב פרמטרים נגזרים כגון שמות
        /// </summary>
        /// <param name="parameters">מילון הפרמטרים המקורי</param>
        /// <returns>מילון מעודכן עם פרמטרים נוספים</returns>
        private async Task<Dictionary<string, ParamValue>> ProcessSpecialParameters(Dictionary<string, ParamValue> parameters)
        {
            // יצירת עותק של מילון הפרמטרים כדי לא לשנות את המקורי
            var enhancedParams = new Dictionary<string, ParamValue>(parameters, StringComparer.OrdinalIgnoreCase);

            try
            {
                // טיפול בפרמטר חודש (mnt) - הוספת שם החודש
                if (enhancedParams.TryGetValue("mnt", out ParamValue mntParam) && mntParam.Value != null)
                {
                    try
                    {
                        int mntValue = Convert.ToInt32(mntParam.Value);
                        string monthName = await _dataAccess.GetMonthName(mntValue);
                        string PeriodName = await _dataAccess.GetPeriodName(mntValue);
                        
                        if (!enhancedParams.ContainsKey("mntname"))
                        {
                            enhancedParams.Add("mntname", new ParamValue(monthName, DbType.String));
                        }
                        
                        if (!enhancedParams.ContainsKey("PeriodName"))
                        {
                            enhancedParams.Add("PeriodName", new ParamValue(monthName, DbType.String));
                        }
                    }
                    catch (Exception ex)
                    {
                        ErrorManager.LogWarning(
                            ErrorCodes.Report.Parameters_Invalid,
                            $"שגיאה בעיבוד פרמטר חודש (mnt): {ex.Message}");
                    }
                }

                // טיפול בפרמטר סוג חיוב (sugts) - הוספת שם סוג החיוב
                if (enhancedParams.TryGetValue("sugts", out ParamValue sugtsParam) && sugtsParam.Value != null)
                {
                    try
                    {
                        int sugtsValue = Convert.ToInt32(sugtsParam.Value);
                        string sugtsName = await _dataAccess.GetSugtsName(sugtsValue);

                        if (!enhancedParams.ContainsKey("sugtsname"))
                        {
                            enhancedParams.Add("sugtsname", new ParamValue(sugtsName, DbType.String));
                        }
                    }
                    catch (Exception ex)
                    {
                        ErrorManager.LogWarning(
                            ErrorCodes.Report.Parameters_Invalid,
                            $"שגיאה בעיבוד פרמטר סוג חיוב (sugts): {ex.Message}");
                    }
                }
                // טיפול בפרמטר רשימת סוגי חיוב (sugtslist)
                else if (enhancedParams.TryGetValue("sugtslist", out ParamValue sugtsListParam) && sugtsListParam.Value != null)
                {
                    try
                    {
                        string sugtsListValue = sugtsListParam.Value.ToString();

                        // בדיקה אם מדובר ברשימה עם ערך אחד בלבד
                        if (!string.IsNullOrEmpty(sugtsListValue) && !sugtsListValue.Contains(","))
                        {
                            // אם יש רק ערך אחד, נשלוף את שם סוג החיוב
                            if (int.TryParse(sugtsListValue, out int singleSugtsValue))
                            {
                                string sugtsName = await _dataAccess.GetSugtsName(singleSugtsValue);

                                if (!enhancedParams.ContainsKey("sugtsname"))
                                {
                                    enhancedParams.Add("sugtsname", new ParamValue(sugtsName, DbType.String));
                                }
                            }
                        }
                        else
                        {
                            // אם יש מספר ערכים, נוסיף פרמטר עם הערה כללית
                            if (!enhancedParams.ContainsKey("sugtsname"))
                            {
                                enhancedParams.Add("sugtsname", new ParamValue("מספר סוגי חיוב", DbType.String));
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        ErrorManager.LogWarning(
                            ErrorCodes.Report.Parameters_Invalid,
                            $"שגיאה בעיבוד פרמטר רשימת סוגי חיוב (sugtslist): {ex.Message}");
                    }
                }
                else
                {
                    // אם לא נמצאו פרמטרים של סוג חיוב, נוסיף ערך ברירת מחדל
                    if (!enhancedParams.ContainsKey("sugtsname"))
                    {
                        enhancedParams.Add("sugtsname", new ParamValue("כל סוגי החיוב", DbType.String));
                    }
                }

                // טיפול בפרמטר יישוב (isvkod) - הוספת שם יישוב
                if (enhancedParams.TryGetValue("isvkod", out ParamValue isvkodParam) && isvkodParam.Value != null)
                {
                    try
                    {
                        string isvkodValue = isvkodParam.Value.ToString();
                    
                        // בדיקה אם מדובר ברשימה או בערך בודד
                        if (!string.IsNullOrEmpty(isvkodValue) && !isvkodValue.Contains(","))
                        {
                            // אם יש רק ערך אחד, נשלוף את שם היישוב
                            if (int.TryParse(isvkodValue, out int singleIsvkodValue))
                            {
                                // קבלת שם היישוב
                                string isvName = await _dataAccess.GetIshvName(singleIsvkodValue);

                                if (!enhancedParams.ContainsKey("ishvname"))
                                {
                                    enhancedParams.Add("ishvname", new ParamValue(isvName, DbType.String));
                                }
                            }
                        }
                        else
                        {
                            // אם יש מספר ערכים, נוסיף פרמטר עם הערה כללית
                            if (!enhancedParams.ContainsKey("ishvname"))
                            {
                                enhancedParams.Add("ishvname", new ParamValue("מספר יישובים", DbType.String));
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        ErrorManager.LogWarning(
                            ErrorCodes.Report.Parameters_Invalid,
                            $"שגיאה בעיבוד פרמטר יישוב (isvkod): {ex.Message}");
                    }
                }
                else
                {
                    // אם לא נמצא פרמטר יישוב, נוסיף ערך ברירת מחדל
                    if (!enhancedParams.ContainsKey("ishvname"))
                    {
                        enhancedParams.Add("ishvname", new ParamValue("כל היישובים", DbType.String));
                    }
                }

                return enhancedParams;
            }
            catch (Exception ex)
            {
                // לוג השגיאה והחזרת הפרמטרים המקוריים
                ErrorManager.LogNormalError(
                    ErrorCodes.Report.Parameters_Invalid,
                    "שגיאה בעיבוד פרמטרים מיוחדים",
                    ex);
                    
                return parameters;
            }
        }
    }
}
