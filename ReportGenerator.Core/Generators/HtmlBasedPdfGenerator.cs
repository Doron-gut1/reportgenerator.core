using ReportGenerator.Core.Data.Models;
using ReportGenerator.Core.Errors;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Threading.Tasks;

namespace ReportGenerator.Core.Generators
{
    /// <summary>
    /// יוצר PDF מבוסס תבניות HTML
    /// </summary>
    public class HtmlBasedPdfGenerator : IDisposable
    {
        private readonly HtmlTemplateManager _templateManager;
        private readonly HtmlTemplateProcessor _templateProcessor;
        private readonly IHtmlToPdfConverter _pdfConverter;
        private bool _disposed = false;

        /// <summary>
        /// יוצר מופע חדש של יוצר PDF מבוסס HTML
        /// </summary>
        /// <param name="templateManager">מנהל תבניות</param>
        /// <param name="templateProcessor">מעבד תבניות</param>
        /// <param name="pdfConverter">ממיר HTML ל-PDF</param>
        public HtmlBasedPdfGenerator(
            HtmlTemplateManager templateManager,
            HtmlTemplateProcessor templateProcessor,
            IHtmlToPdfConverter pdfConverter)
        {
            _templateManager = templateManager ?? throw new ArgumentNullException(nameof(templateManager));
            _templateProcessor = templateProcessor ?? throw new ArgumentNullException(nameof(templateProcessor));
            _pdfConverter = pdfConverter ?? throw new ArgumentNullException(nameof(pdfConverter));
        }

        /// <summary>
        /// מייצר PDF מתבנית HTML
        /// </summary>
        /// <param name="templateName">שם התבנית</param>
        /// <param name="reportTitle">כותרת הדוח</param>
        /// <param name="dataTables">טבלאות נתונים</param>
        /// <param name="parameters">פרמטרים שהועברו לדוח</param>
        /// <returns>מערך בייטים של קובץ PDF</returns>
        public async Task<byte[]> GenerateFromTemplate(
            string templateName,
            string reportTitle,
            Dictionary<string, DataTable> dataTables,
            Dictionary<string, ParamValue> parameters)
        {
            try
            {
                // הכנת מילון ערכים בסיסי
                Dictionary<string, object> values = new Dictionary<string, object>
                {
                    { "ReportTitle", reportTitle },
                    { "CurrentDate", DateTime.Now.ToString("dd/MM/yyyy") },
                    { "CurrentTime", DateTime.Now.ToString("HH:mm") }
                };

                // הוספת פרמטרים למילון הערכים
                foreach (var param in parameters)
                {
                    values[param.Key] = param.Value.Value ?? "";
                }

                // 1. טעינת התבנית
                string templateHtml = await _templateManager.GetTemplateAsync(templateName);

                // 2. עיבוד התבנית - הערה: מעבד התבנית מטפל עכשיו גם בפלייסהולדרים של מספור עמודים
                string processedHtml = _templateProcessor.ProcessTemplate(templateHtml, values, dataTables);

                // 3. חילוץ כותרות עליונות ותחתונות
                string headerHtml = _pdfConverter.ExtractHeaderFragment(processedHtml);
                string footerHtml = _pdfConverter.ExtractFooterFragment(processedHtml);
                
                // כתיבת ה-HTML המעובד לקובץ לצורכי דיבוג (רק בסביבת פיתוח)
                #if DEBUG
                File.WriteAllText($"{templateName}_processed.html", processedHtml);
                #endif
               
                // 4. המרה ל-PDF
                byte[] pdfBytes;
                if (!string.IsNullOrEmpty(headerHtml) || !string.IsNullOrEmpty(footerHtml))
                {
                    // אם יש כותרות, נשתמש בממיר עם כותרות מותאמות
                    pdfBytes = await _pdfConverter.ConvertToPdfWithCustomHeaderFooter(
                        processedHtml, reportTitle, headerHtml, footerHtml);
                }
                else
                {
                    // אחרת נשתמש בממיר הרגיל
                    pdfBytes = await _pdfConverter.ConvertToPdf(processedHtml, reportTitle);
                }

                // רישום לוג של הצלחה
                ErrorManager.LogInfo(
                    "PDF_Generation_Success",
                    $"קובץ PDF נוצר בהצלחה עבור דוח {templateName}. גודל: {pdfBytes.Length / 1024:N0} KB",
                    reportName: templateName);

                return pdfBytes;
            }
            catch (Exception ex) when (ex is not ArgumentNullException)
            {
                // הוספת שגיאות ספציפיות יותר לפי סוג החריגה
                if (ex.Message.Contains("Template") || ex.Message.Contains("תבנית"))
                {
                    ErrorManager.LogError(
                        ErrorCodes.Template.Not_Found,
                        ErrorSeverity.Critical,
                        $"שגיאה בטעינת תבנית {templateName}",
                        ex,
                        reportName: templateName);
                }
                else if (ex.Message.Contains("Chrome") || ex.Message.Contains("Browser"))
                {
                    ErrorManager.LogError(
                        ErrorCodes.PDF.Chrome_Not_Found,
                        ErrorSeverity.Critical,
                        "שגיאה בטעינת דפדפן Chrome להמרת PDF",
                        ex,
                        reportName: templateName);
                }
                else
                {
                    ErrorManager.LogError(
                        ErrorCodes.PDF.Generation_Failed,
                        ErrorSeverity.Critical,
                        $"שגיאה כללית ביצירת PDF עבור דוח {templateName}",
                        ex,
                        reportName: templateName);
                }

                throw new Exception($"Error generating PDF from template {templateName}: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// שחרור משאבים
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// שחרור משאבים - מימוש פנימי
        /// </summary>
        /// <param name="disposing">האם לשחרר משאבים מנוהלים</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // שחרור משאבים מנוהלים
                    _pdfConverter?.Dispose();
                }

                _disposed = true;
            }
        }

        /// <summary>
        /// פינאלייזר
        /// </summary>
        ~HtmlBasedPdfGenerator()
        {
            Dispose(false);
        }
    }
}