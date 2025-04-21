using System;
using System.Threading.Tasks;
using System.IO;
using System.Collections.Generic;
using Microsoft.Extensions.Options;
using ReportGenerator.Core.Configuration;
using ReportGenerator.Core.Data.Models;
using ReportGenerator.Core.Errors;
using ReportGenerator.Core.Interfaces;
using ReportGenerator.Core.Management.Enums;
using ReportGenerator.Core.Management.Services;

namespace ReportGenerator.Core.Management
{
    /// <summary>
    /// מנהל הדוחות הראשי - מקשר בין כל רכיבי המערכת
    /// </summary>
    public class ReportManager : IReportGenerator
    {
        private readonly IDataAccess _dataAccess;
        private readonly IErrorManager _errorManager;
        private readonly ReportSettings _settings;

        // שירותים פנימיים
        private readonly ParameterProcessor _parameterProcessor;
        private readonly ReportExecutor _reportExecutor;
        private readonly ReportOutputManager _outputManager;

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
            _errorManager = errorManager ?? throw new ArgumentNullException(nameof(errorManager));
            _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));

            // יצירת מחלקות השירות
            _parameterProcessor = new ParameterProcessor(dataAccess, errorManager);
            _reportExecutor = new ReportExecutor(dataAccess, errorManager);
            _outputManager = new ReportOutputManager(
                templateManager,
                pdfGenerator,
                excelGenerator,
                errorManager,
                settings);
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
                    byte[] reportData = await GenerateReport(reportName, format, parameters);

                    // שמירה לקובץ
                    _outputManager.SaveReportToFile(reportName, format, reportData);
                }
                catch (Exception ex)
                {
                    _errorManager.LogCriticalError(
                        ErrorCode.Report_Generation_Failed,
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
                ErrorCode.General_Info,
                $"התחלת הפקת דוח {reportName} בפורמט {format}");

            // מעקב אחר משך זמן ההפקה
            var startTime = DateTime.Now;

            try
            {
                // 1. קבלת הגדרות הדוח הבסיסיות
                var tempConfig = await _dataAccess.GetReportConfig(reportName);
                string procName = tempConfig.StoredProcName.Split(';')[0].Trim(); // קבלת הפרוצדורה הראשונה

                // 2. עיבוד פרמטרים
                var parsedParams = await _parameterProcessor.ProcessParameters(reportName, procName, parameters);

                // 3. הרצת הדוח
                var (reportConfig, dataTables) = await _reportExecutor.ExecuteReport(reportName, parsedParams);

                // 4. יצירת פלט
                byte[] result = await _outputManager.CreateOutput(
                    reportName,
                    reportConfig.Title,
                    format,
                    dataTables,
                    parsedParams);

                // 5. רישום סיום מוצלח
                var duration = DateTime.Now - startTime;
                _errorManager.LogInfo(
                    ErrorCode.General_Info,
                    $"הפקת דוח {reportName} הסתיימה בהצלחה בפורמט {format}. " +
                    $"משך: {duration.TotalSeconds:F2} שניות. גודל: {result.Length / 1024:N0} KB",
                    reportName: reportName);

                return result;
            }
            catch (Exception ex)
            {
                // במקרה של שגיאה, רשום אותה ופרטים נוספים
                _errorManager.LogCriticalError(
                    ErrorCode.Report_Generation_Failed,
                    $"שגיאה בהפקת דוח {reportName}",
                    ex,
                    reportName: reportName);

                throw new Exception($"Error generating report {reportName}: {ex.Message}", ex);
            }
        }
    }
}
