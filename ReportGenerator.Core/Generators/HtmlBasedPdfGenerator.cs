using ReportGenerator.Core.Data.Models;
using ReportGenerator.Core.Errors;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

namespace ReportGenerator.Core.Generators
{
    /// <summary>
    /// מחולל PDF מבוסס HTML - משתמש בתבניות HTML ליצירת קבצי PDF
    /// </summary>
    public class HtmlBasedPdfGenerator
    {
        private readonly HtmlTemplateManager _templateManager;
        private readonly HtmlTemplateProcessor _templateProcessor;
        private readonly IHtmlToPdfConverter _htmlToPdfConverter;

        /// <summary>
        /// יוצר מופע חדש של מחולל ה-PDF
        /// </summary>
        /// <param name="templateManager">מנהל תבניות HTML</param>
        /// <param name="templateProcessor">מעבד תבניות HTML</param>
        /// <param name="htmlToPdfConverter">ממיר HTML ל-PDF</param>
        public HtmlBasedPdfGenerator(
            HtmlTemplateManager templateManager,
            HtmlTemplateProcessor templateProcessor,
            IHtmlToPdfConverter htmlToPdfConverter)
        {
            _templateManager = templateManager ?? throw new ArgumentNullException(nameof(templateManager));
            _templateProcessor = templateProcessor ?? throw new ArgumentNullException(nameof(templateProcessor));
            _htmlToPdfConverter = htmlToPdfConverter ?? throw new ArgumentNullException(nameof(htmlToPdfConverter));
        }

        /// <summary>
        /// יצירת קובץ PDF מתבנית HTML
        /// </summary>
        /// <param name="templateName">שם התבנית (ללא סיומת)</param>
        /// <param name="reportTitle">כותרת הדוח</param>
        /// <param name="dataTables">מקורות הנתונים לדוח</param>
        /// <param name="parameters">פרמטרים נוספים להחלפה בתבנית</param>
        /// <returns>מערך בייטים של קובץ ה-PDF</returns>
        public async Task<byte[]> GenerateFromTemplate(
            string templateName,
            string reportTitle,
            Dictionary<string, DataTable> dataTables,
            Dictionary<string, ParamValue> parameters = null)
        {
            try
            {
                // 1. טעינת תבנית HTML
                string templateHtml = await _templateManager.GetTemplateAsync(templateName);
                
                // 2. עיבוד התבנית והחלפת פלייסהולדרים
                string processedHtml = _templateProcessor.ProcessTemplate(
                    templateHtml, reportTitle, dataTables, parameters);
                
                // 3. המרת HTML ל-PDF
                byte[] pdfBytes = await _htmlToPdfConverter.ConvertToPdfAsync(processedHtml);
                
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
                
                throw new Exception($"Failed to generate PDF from template {templateName}", ex);
            }
        }
    }
}
