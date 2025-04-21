using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using ReportGenerator.Core.Configuration;
using ReportGenerator.Core.Data.Models;
using ReportGenerator.Core.Errors;
using ReportGenerator.Core.Interfaces;
using ReportGenerator.Core.Management.Enums;

namespace ReportGenerator.Core.Management.Services
{
    /// <summary>
    /// מחלקה לטיפול ביצירת ושמירת קבצי פלט של דוחות
    /// </summary>
    internal class ReportOutputManager
    {
        private readonly ITemplateManager _templateManager;
        private readonly IPdfGenerator _pdfGenerator;
        private readonly IExcelGenerator _excelGenerator;
        private readonly IErrorManager _errorManager;
        private readonly ReportSettings _settings;

        /// <summary>
        /// יוצר מנהל פלט חדש
        /// </summary>
        public ReportOutputManager(
            ITemplateManager templateManager,
            IPdfGenerator pdfGenerator,
            IExcelGenerator excelGenerator,
            IErrorManager errorManager,
            IOptions<ReportSettings> settings)
        {
            _templateManager = templateManager ?? throw new ArgumentNullException(nameof(templateManager));
            _pdfGenerator = pdfGenerator ?? throw new ArgumentNullException(nameof(pdfGenerator));
            _excelGenerator = excelGenerator ?? throw new ArgumentNullException(nameof(excelGenerator));
            _errorManager = errorManager ?? throw new ArgumentNullException(nameof(errorManager));
            _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
        }

        /// <summary>
        /// קונסטרקטור נוסף עבור מקרים שבהם ReportSettings כבר זמין
        /// </summary>
        public ReportOutputManager(
            ITemplateManager templateManager,
            IPdfGenerator pdfGenerator,
            IExcelGenerator excelGenerator,
            IErrorManager errorManager,
            ReportSettings settings)
        {
            _templateManager = templateManager ?? throw new ArgumentNullException(nameof(templateManager));
            _pdfGenerator = pdfGenerator ?? throw new ArgumentNullException(nameof(pdfGenerator));
            _excelGenerator = excelGenerator ?? throw new ArgumentNullException(nameof(excelGenerator));
            _errorManager = errorManager ?? throw new ArgumentNullException(nameof(errorManager));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        /// <summary>
        /// יוצר קובץ פלט לפי הפורמט המבוקש
        /// </summary>
        public async Task<byte[]> CreateOutput(
            string reportName, 
            string reportTitle, 
            OutputFormat format,
            Dictionary<string, System.Data.DataTable> dataTables, 
            Dictionary<string, ParamValue> parameters)
        {
            if (format == OutputFormat.PDF)
            {
                // וידוא שתבנית HTML קיימת
                if (!_templateManager.TemplateExists(reportName))
                {
                    _errorManager.LogCriticalError(
                        ErrorCode.Template_Not_Found,
                        $"לא נמצאה תבנית HTML עבור דוח {reportName}. יש ליצור קובץ תבנית בשם '{reportName}.html'",
                        reportName: reportName);

                    throw new FileNotFoundException($"No HTML template found for report {reportName}. Please create an HTML template file named '{reportName}.html'");
                }

                // שימוש בגישה מבוססת HTML
                return await _pdfGenerator.GenerateFromTemplate(
                    reportName, reportTitle, dataTables, parameters);
            }
            else // Excel
            {
                // יצירת קובץ אקסל עם כל הנתונים
                return _excelGenerator.Generate(dataTables, reportTitle);
            }
        }

        /// <summary>
        /// שומר את הדוח לקובץ פיזי בדיסק
        /// </summary>
        public void SaveReportToFile(string reportName, OutputFormat format, byte[] reportData)
        {
            try
            {
                // שימוש בהגדרות מקובץ קונפיגורציה
                var outputFolder = _settings.OutputFolder;
                
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
                    ErrorCode.General_Info,
                    $"הדוח {reportName} נשמר בהצלחה בנתיב {fullPath}",
                    reportName: reportName);
            }
            catch (Exception ex)
            {
                _errorManager.LogCriticalError(
                    ErrorCode.Report_Save_Failed,
                    $"שגיאה בשמירת דוח {reportName}",
                    ex,
                    reportName: reportName);
                throw;
            }
        }
    }
}