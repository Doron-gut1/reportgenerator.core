using System;
using System.Threading.Tasks;
using System.Data;
using System.Collections.Generic;
using ReportGenerator.Core.Data;
using ReportGenerator.Core.Data.Models;
using ReportGenerator.Core.Generators;
using ReportGenerator.Core.Management.Enums;
using DinkToPdf;
using DinkToPdf.Contracts;
using System.IO;

namespace ReportGenerator.Core.Management
{
    /// <summary>
    /// מנהל הדוחות הראשי - מקשר בין כל רכיבי המערכת
    /// </summary>
    public class ReportManager
    {
        private readonly DataAccess _dataAccess;
        private readonly HtmlTemplateManager _templateManager;
        private readonly HtmlTemplateProcessor _templateProcessor;
        private readonly HtmlBasedPdfGenerator _htmlPdfGenerator;
        private readonly ExcelGenerator _excelGenerator;
        private readonly PdfGenerator _legacyPdfGenerator; // לתאימות אחורה

        /// <summary>
        /// יוצר מופע חדש של מנהל הדוחות
        /// </summary>
        /// <param name="connectionString">מחרוזת התחברות לבסיס הנתונים</param>
        /// <param name="templatesFolder">נתיב לתיקיית תבניות HTML</param>
        /// <param name="legacyPdfTemplatePath">נתיב לתבניות PDF ישנות (אופציונלי)</param>
        public ReportManager(string connectionString, string templatesFolder, string legacyPdfTemplatePath = null)
        {
            _dataAccess = new DataAccess(connectionString);
            
            // הגדרת רכיבי מערכת התבניות החדשה
            _templateManager = new HtmlTemplateManager(templatesFolder);
            
            // יצירת מופע של DinkToPdf
            var converter = new SynchronizedConverter(new PdfTools());
            var pdfConverter = new HtmlToPdfConverter(converter);
            
            // יוצר הPDF החדש (מבוסס HTML)
            _htmlPdfGenerator = new HtmlBasedPdfGenerator(_templateManager, null, pdfConverter);
            
            // יוצר אקסל ללא מיפויי כותרות בשלב זה (יוגדרו מאוחר יותר)
            _excelGenerator = new ExcelGenerator();
            
            // מחלקת הPDF הישנה (לתאימות אחורה)
            if (!string.IsNullOrEmpty(legacyPdfTemplatePath))
            {
                _legacyPdfGenerator = new PdfGenerator(legacyPdfTemplatePath);
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
                    // בדיקה אם יש תבנית HTML לשם הדוח
                    if (_templateManager.TemplateExists(reportName))
                    {
                        // שימוש בגישה החדשה מבוססת HTML
                        return await _htmlPdfGenerator.GenerateFromTemplate(
                            reportName, reportConfig.Title, dataTables, parsedParams);
                    }
                    else if (_legacyPdfGenerator != null)
                    {
                        // תאימות אחורה - שימוש בגישה הישנה מבוססת PDF Forms
                        // ממזג את כל הנתונים לטבלה אחת
                        DataTable mergedData = MergeDataTables(dataTables);
                        return _legacyPdfGenerator.Generate(mergedData);
                    }
                    else
                    {
                        throw new Exception($"No template found for report {reportName}");
                    }
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
        /// מיזוג מילון טבלאות נתונים לטבלה אחת (לתאימות אחורה)
        /// </summary>
        private DataTable MergeDataTables(Dictionary<string, DataTable> dataTables)
        {
            if (dataTables.Count == 0)
                throw new ArgumentException("No data tables provided");

            // אם יש רק טבלה אחת, נחזיר אותה
            if (dataTables.Count == 1)
                return dataTables.Values.First();

            // אחרת, נמזג את כל הטבלאות
            DataTable result = new DataTable();
            
            foreach (var table in dataTables.Values)
            {
                // הוספת עמודות חדשות
                foreach (DataColumn col in table.Columns)
                {
                    string colName = col.ColumnName;
                    if (!result.Columns.Contains(colName))
                    {
                        result.Columns.Add(colName, col.DataType);
                    }
                }
                
                // הוספת שורות
                foreach (DataRow row in table.Rows)
                {
                    DataRow newRow = result.NewRow();
                    
                    foreach (DataColumn col in table.Columns)
                    {
                        newRow[col.ColumnName] = row[col];
                    }
                    
                    result.Rows.Add(newRow);
                }
            }
            
            return result;
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
