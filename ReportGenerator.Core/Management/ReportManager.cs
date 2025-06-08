using System;
using System.Data;
using System.Threading.Tasks;
using System.IO;
using System.Collections.Generic;
using ReportGenerator.Core.Configuration;
using ReportGenerator.Core.Data.Models;
using ReportGenerator.Core.Data;
using ReportGenerator.Core.Errors;

using ReportGenerator.Core.Management.Enums;
using ReportGenerator.Core.Generators;


namespace ReportGenerator.Core.Management
{
    /// <summary>
    /// מנהל הדוחות הראשי - מקשר בין כל רכיבי המערכת
    /// </summary>
    public class ReportManager
    {
        private readonly DataAccess _dataAccess;
        //private readonly ErrorManager SimpleLogger;
        private readonly ReportSettings _settings;
        private readonly HtmlTemplateManager _templateManager;
        private readonly HtmlTemplateProcessor _templateProcessor;
        private readonly HtmlBasedPdfGenerator _pdfGenerator;
        private readonly ExcelGenerator _excelGenerator;

        /// <summary>
        /// יוצר מופע חדש של מנהל הדוחות
        /// </summary>
        public ReportManager(
            string connectionString,
            string templatesFolder,
            string outputFolder,
            IHtmlToPdfConverter htmlToPdfConverter = null)
        {
            _settings = new ReportSettings
            {
                ConnectionString = connectionString,
                TemplatesFolder = templatesFolder,
                OutputFolder = outputFolder
            };

            // יצירה ישירה של כל התלויות
            SimpleLogger.Initialize(connectionString);
            _dataAccess = new DataAccess(Microsoft.Extensions.Options.Options.Create(_settings));
            _templateManager = new HtmlTemplateManager(Microsoft.Extensions.Options.Options.Create(_settings));
            _templateProcessor = new HtmlTemplateProcessor();
            _pdfGenerator = new HtmlBasedPdfGenerator(
                _templateManager,
                _templateProcessor,
                htmlToPdfConverter ?? new PuppeteerHtmlToPdfConverter());
            
            // קבלת מיפויי עמודות
            var columnMappings = _dataAccess.GetDefaultColumnMappings().GetAwaiter().GetResult();
            _templateProcessor.SetColumnMappings(columnMappings);
            _excelGenerator = new ExcelGenerator(columnMappings);
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
                    // הפקת הדוח באמצעות המתודה הקיימת
                    byte[] reportData = await GenerateReportBytesOnly(reportName, format, parameters);

                    // שמירה לקובץ
                    SaveReportToFile(reportName, format, reportData);
                }
                catch (Exception ex)
                {
                    SimpleLogger.LogCriticalError($"שגיאה בהפקת ושמירת דוח {reportName}", ex, reportName: reportName);
                }
            });
        }

        /// <summary>
        /// מייצר דוח ומחזיר bytes בלבד - ללא שמירה לדיסק
        /// </summary>
        /// <param name="reportName">שם הדוח</param>
        /// <param name="format">פורמט הפלט (PDF/Excel)</param>
        /// <param name="parameters">פרמטרים לדוח</param>
        /// <returns>מערך בייטים של הקובץ המבוקש</returns>
        public async Task<byte[]> GenerateReportBytesOnly(string reportName, OutputFormat format, params object[] parameters)
        {
            // ניקוי שגיאות מהפקות קודמות
           // SimpleLogger.ClearErrors();

            // רישום תחילת הפקת הדוח
            SimpleLogger.LogInfo( $"התחלת הפקת דוח {reportName} בפורמט {format}");

            // מעקב אחר משך זמן ההפקה
            var startTime = DateTime.Now;

            try
            {
                // 1. קבלת הגדרות הדוח
                var reportConfig = await _dataAccess.GetReportConfig(reportName);
                
                // 2. עיבוד פרמטרים
                var parsedParams = ProcessParameters(parameters);
                
                // 3. הרצת הדוח
                var dataTables = await _dataAccess.ExecuteMultipleStoredProcedures(
                    reportConfig.StoredProcName, 
                    parsedParams);
                
                // 4. יצירת פלט
                byte[] result = await CreateOutput(
                    reportName,
                    reportConfig.Title,
                    format,
                    dataTables,
                    parsedParams);

                // 5. רישום סיום מוצלח
                var duration = DateTime.Now - startTime;
                SimpleLogger.LogInfo( $"הפקת דוח {reportName} (bytes only) הסתיימה בהצלחה בפורמט {format}. " +
                    $"משך: {duration.TotalSeconds:F2} שניות. גודל: {result.Length / 1024:N0} KB",
                    reportName: reportName);

                return result;
            }
            catch (Exception ex)
            {
                // במקרה של שגיאה, רשום אותה ופרטים נוספים
                SimpleLogger.LogCriticalError( $"שגיאה בהפקת דוח {reportName} (bytes only)", ex, reportName: reportName);
                throw new Exception($"Error generating report {reportName} (bytes only): {ex.Message}", ex);
            }
        }

        /// <summary>
        /// מייצר דוח, שומר לדיסק, ומחזיר bytes
        /// </summary>
        /// <param name="reportName">שם הדוח</param>
        /// <param name="format">פורמט הפלט (PDF/Excel)</param>
        /// <param name="savePath">נתיב שמירה מותאם (אופציונלי)</param>
        /// <param name="parameters">פרמטרים לדוח</param>
        /// <returns>מערך בייטים של הקובץ המבוקש</returns>
        public async Task<byte[]> GenerateReportAndSave(string reportName, OutputFormat format, string? savePath = null, params object[] parameters)
        {
            // הפקה ללא שמירה
            byte[] result = await GenerateReportBytesOnly(reportName, format, parameters);
            
            // שמירה מפורשת
            SaveReportToFile(reportName, format, result, savePath);
            
            SimpleLogger.LogInfo( $"הדוח {reportName} הופק ונשמר בהצלחה בנתיב: {savePath ?? _settings.OutputFolder}", reportName: reportName);
            
            return result;
        }

        /// <summary>
        /// מייצר דוח לפי שם, פורמט ופרמטרים
        /// הפונקציה הקיימת - נשמרת לתאימות אחורה
        /// מתנהגת לפי הגדרות הקונפיגורציה (SaveOutputFiles)
        /// </summary>
        /// <param name="reportName">שם הדוח</param>
        /// <param name="format">פורמט הפלט (PDF/Excel)</param>
        /// <param name="parameters">פרמטרים לדוח</param>
        /// <returns>מערך בייטים של הקובץ המבוקש</returns>
        [Obsolete("השתמש ב-GenerateReportBytesOnly או GenerateReportAndSave לבהירות מלאה")]
        public async Task<byte[]> GenerateReport(string reportName, OutputFormat format, params object[] parameters)
        {
            return await GenerateReportBytesOnly(reportName, format, parameters);
        }

        /// <summary>
        /// עיבוד פרמטרים שהועברו מה-Access
        /// </summary>
        private Dictionary<string, ParamValue> ProcessParameters(object[] parameters)
        {
            var result = new Dictionary<string, ParamValue>();
            
            if (parameters == null || parameters.Length == 0)
                return result;
                
            // פרמטרים מגיעים בפורמט: שם, ערך, DbType
            for (int i = 0; i < parameters.Length; i += 3)
            {
                if (i + 2 < parameters.Length)
                {
                    string name = parameters[i].ToString();
                    object value = parameters[i + 1];
                    var dbType = (System.Data.DbType)parameters[i + 2];
                    
                    result[name] = new ParamValue(value, dbType);
                }
            }
            
            return result;
        }

        /// <summary>
        /// יצירת הפלט הסופי (PDF או Excel)
        /// </summary>
        private async Task<byte[]> CreateOutput(
            string reportName,
            string reportTitle,
            OutputFormat format,
            Dictionary<string, DataTable> dataTables,
            Dictionary<string, ParamValue> parameters)
        {
            try
            {
                return format switch
                {
                    OutputFormat.PDF => await CreatePdfOutput(reportName, reportTitle, dataTables, parameters),
                    OutputFormat.Excel => CreateExcelOutput(reportTitle, dataTables),
                    _ => throw new ArgumentException($"Unsupported format: {format}")
                };
            }
            catch (Exception ex)
            {
                SimpleLogger.LogCriticalError($"שגיאה ביצירת פלט עבור דוח {reportName}",ex,reportName: reportName);
                throw;
            }
        }

        /// <summary>
        /// יצירת PDF
        /// </summary>
        private async Task<byte[]> CreatePdfOutput(
            string reportName,
            string reportTitle,
            Dictionary<string, DataTable> dataTables,
            Dictionary<string, ParamValue> parameters)
        {
            return await _pdfGenerator.GenerateFromTemplate(
                reportName,
                reportTitle,
                dataTables,
                parameters);
        }

        /// <summary>
        /// יצירת Excel
        /// </summary>
        private byte[] CreateExcelOutput(
            string reportTitle,
            Dictionary<string, DataTable> dataTables)
        {
            return _excelGenerator.Generate(dataTables, reportTitle);
        }

        /// <summary>
        /// שמירת הדוח לקובץ
        /// </summary>
        private void SaveReportToFile(string reportName, OutputFormat format, byte[] data, string customPath = null)
        {
            try
            {
                string fileName = $"{reportName}_{DateTime.Now:yyyyMMdd_HHmmss}";
                string extension = format == OutputFormat.PDF ? ".pdf" : ".xlsx";
                string outputPath = customPath ?? _settings.OutputFolder;
                
                if (!Directory.Exists(outputPath))
                    Directory.CreateDirectory(outputPath);
                    
                string fullPath = Path.Combine(outputPath, fileName + extension);
                File.WriteAllBytes(fullPath, data);
                
                SimpleLogger.LogInfo( $"דוח נשמר בהצלחה: {fullPath}", reportName: reportName);
            }
            catch (Exception ex)
            {
                SimpleLogger.LogError( $"שגיאה בשמירת דוח {reportName}",ex,reportName: reportName);
                throw;
            }
        }
    }
}
