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

        /// <summary>
        /// יוצר מופע חדש של מנהל הדוחות
        /// </summary>
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

        /// <summary>
        /// מייצר דוח לפי שם, פורמט ופרמטרים
        /// </summary>
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

                // עדכון מעבד התבניות עם המיפויים
                _templateProcessor = new HtmlTemplateProcessor(columnMappings);

                // עדכון מחלקת האקסל עם המיפויים
                _excelGenerator = new ExcelGenerator(columnMappings);

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

        /// <summary>
        /// המרת מערך פרמטרים לפורמט המובן למערכת
        /// </summary>
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
    }
}