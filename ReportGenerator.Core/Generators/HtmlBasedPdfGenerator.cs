using ReportGenerator.Core.Data.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

namespace ReportGenerator.Core.Generators
{
    /// <summary>
    /// יוצר PDF מבוסס תבניות HTML
    /// </summary>
    public class HtmlBasedPdfGenerator
    {
        private readonly HtmlTemplateManager _templateManager;
        private readonly HtmlTemplateProcessor _templateProcessor;
        private readonly IHtmlToPdfConverter _pdfConverter;

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

                // 2. עיבוד התבנית
                string processedHtml = _templateProcessor.ProcessTemplate(templateHtml, values, dataTables);

                // 3. חילוץ כותרות עליונות ותחתונות
                string headerHtml = _pdfConverter.ExtractHeaderFragment(processedHtml);
                string footerHtml = _pdfConverter.ExtractFooterFragment(processedHtml);

                // 4. המרה ל-PDF
                if (!string.IsNullOrEmpty(headerHtml) || !string.IsNullOrEmpty(footerHtml))
                {
                    // אם יש כותרות, נשתמש בממיר עם כותרות מותאמות
                    return await _pdfConverter.ConvertToPdfWithCustomHeaderFooter(
                        processedHtml, reportTitle, headerHtml, footerHtml);
                }
                else
                {
                    // אחרת נשתמש בממיר הרגיל
                    return await _pdfConverter.ConvertToPdf(processedHtml, reportTitle);
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error generating PDF from template {templateName}: {ex.Message}", ex);
            }
        }
    }
}