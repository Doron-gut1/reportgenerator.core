using System;
using System.Threading.Tasks;
using System.Data;
using System.Collections.Generic;
using ReportGenerator.Core.Data;
using ReportGenerator.Core.Data.Models;
using ReportGenerator.Core.Generators;
using ReportGenerator.Core.Management.Enums;

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


        /// יוצר מופע חדש של מנהל הדוחות

        /// <param name="connectionString">מחרוזת התחברות לבסיס הנתונים</param>
        /// <param name="templatesFolder">נתיב לתיקיית תבניות HTML</param>
        /// <param name="chromePath">נתיב לקובץ ההפעלה של Chrome (אופציונלי)</param>
        public ReportManager(string connectionString, string templatesFolder, string chromePath = null)
        {
            _dataAccess = new DataAccess(connectionString);
            
            // הגדרת רכיבי מערכת התבניות החדשה
            _templateManager = new HtmlTemplateManager(templatesFolder);
            
            // יצירת מופע ראשוני של מעבד התבניות עם מילון ריק
            _templateProcessor = new HtmlTemplateProcessor(new Dictionary<string, string>());
            
            // יצירת ממיר HTML ל-PDF עם PuppeteerSharp
            var pdfConverter = new PuppeteerHtmlToPdfConverter(chromePath);
            
            // יוצר ה-PDF מבוסס HTML
            _htmlPdfGenerator = new HtmlBasedPdfGenerator(_templateManager, _templateProcessor, pdfConverter);
            
            // יוצר אקסל ללא מיפויי כותרות בשלב זה (יוגדרו מאוחר יותר)
            _excelGenerator = new ExcelGenerator();
        }


        /// מייצר דוח לפי שם, פורמט ופרמטרים

        /// <param name="reportName">שם הדוח</param>
        /// <param name="format">פורמט הפלט (PDF/Excel)</param>
        /// <param name="parameters">פרמטרים לדוח</param>
        /// <returns>מערך בייטים של הקובץ המבוקש</returns>
        public async Task<byte[]> GenerateReport(string reportName, OutputFormat format, params object[] parameters)
        {
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
                if (format == OutputFormat.PDF)
                {
                    // וידוא שתבנית HTML קיימת
                    if (!_templateManager.TemplateExists(reportName))
                    {
                        throw new Exception($"No HTML template found for report {reportName}. Please create an HTML template file named '{reportName}.html'");
                    }

                    // שימוש בגישה החדשה מבוססת HTML
                    return await _htmlPdfGenerator.GenerateFromTemplate(
                        reportName, reportConfig.Title, dataTables, parsedParams);
                }
                else // Excel
                {
                    // יצירת קובץ אקסל עם כל הנתונים
                    return _excelGenerator.Generate(dataTables, reportConfig.Title);
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error generating report {reportName}: {ex.Message}", ex);
            }
        }


        /// המרת מערך פרמטרים לפורמט המובן למערכת

        private Dictionary<string, ParamValue> ParseParameters(object[] paramArray)
        {
            var result = new Dictionary<string, ParamValue>(StringComparer.OrdinalIgnoreCase);
            
            try
            {
                for (int i = 0; i < paramArray.Length; i += 3)
                {
                    if (i + 2 >= paramArray.Length)
                        throw new ArgumentException("Parameter array is not in the correct format");

                    string paramName = paramArray[i]?.ToString() ??
                        throw new ArgumentException($"Parameter name at position {i} is null");

                    object paramValue = paramArray[i + 1];
                    DbType paramType = (DbType)paramArray[i + 2];

                    result.Add(paramName, new ParamValue(paramValue, paramType));
                }
                
                return result;
            }
            catch (Exception ex)
            {
                throw new ArgumentException("Error parsing parameters array", ex);
            }
        }


        /// מטפל בפרמטרים מיוחדים ומחשב פרמטרים נגזרים כגון שמות

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

                // טיפול בפרמטר סוג חיוב (sugts) - הוספת שם סוג החיוב
                if (enhancedParams.TryGetValue("sugts", out ParamValue sugtsParam) && sugtsParam.Value != null)
                {
                    int sugtsValue = Convert.ToInt32(sugtsParam.Value);
                    string sugtsName = await _dataAccess.GetSugtsName(sugtsValue);

                    if (!enhancedParams.ContainsKey("sugtsname"))
                    {
                        enhancedParams.Add("sugtsname", new ParamValue(sugtsName, DbType.String));
                    }
                }
                // טיפול בפרמטר רשימת סוגי חיוב (sugtslist)
                else if (enhancedParams.TryGetValue("sugtslist", out ParamValue sugtsListParam) && sugtsListParam.Value != null)
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
                    string isvkodValue = isvkodParam.Value.ToString();
                
                    // בדיקה אם מדובר ברשימה או בערך בודד
                    if (!string.IsNullOrEmpty(isvkodValue) && !isvkodValue.Contains(","))
                    {
                        // אם יש רק ערך אחד, נשלוף את שם היישוב
                        if (int.TryParse(isvkodValue, out int singleIsvkodValue))
                        {
                            // יש להוסיף מתודה לשליפת שם יישוב
                            // string isvName = await _dataAccess.GetIsvName(singleIsvkodValue);
                            isvkodValue= await _dataAccess.GetIshvName(singleIsvkodValue);
                            string isvName = $"יישוב {singleIsvkodValue}"; // כברירת מחדל עד שתיווצר המתודה

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
                Console.WriteLine($"שגיאה בעיבוד פרמטרים מיוחדים: {ex.Message}");
                return parameters;
            }
        }
    }
}