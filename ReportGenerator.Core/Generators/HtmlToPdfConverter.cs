using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DinkToPdf;
using DinkToPdf.Contracts;

namespace ReportGenerator.Core.Generators
{
    /// <summary>
    /// ממיר HTML ל-PDF
    /// </summary>
    public class HtmlToPdfConverter
    {
        private readonly IConverter _converter;

        /// <summary>
        /// יוצר מופע חדש של ממיר HTML ל-PDF
        /// </summary>
        /// <param name="converter">מופע של ממיר DinkToPdf</param>
        public HtmlToPdfConverter(IConverter converter)
        {
            _converter = converter ?? throw new ArgumentNullException(nameof(converter));
        }

        /// <summary>
        /// ממיר HTML ל-PDF
        /// </summary>
        /// <param name="html">תוכן HTML</param>
        /// <param name="documentTitle">כותרת המסמך</param>
        /// <returns>מערך בייטים של PDF</returns>
        public byte[] ConvertToPdf(string html, string documentTitle = "Report")
        {
            var globalSettings = new GlobalSettings
            {
                ColorMode = ColorMode.Color,
                Orientation = Orientation.Portrait,
                PaperSize = PaperKind.A4,
                Margins = new MarginSettings { Top = 10, Bottom = 10, Left = 10, Right = 10 },
                DocumentTitle = documentTitle,
            };

            var objectSettings = new ObjectSettings
            {
                PagesCount = true,
                HtmlContent = html,
                WebSettings = { DefaultEncoding = "utf-8", EnableJavascript = false },
                HeaderSettings = { FontSize = 9, Right = "עמוד [page] מתוך [toPage]", Line = true },
                FooterSettings = { FontSize = 9, Line = true, Center = DateTime.Now.ToString("dd/MM/yyyy HH:mm") }
            };

            var pdf = new HtmlToPdfDocument()
            {
                GlobalSettings = globalSettings,
                Objects = { objectSettings }
            };

            return _converter.Convert(pdf);
        }

        /// <summary>
        /// ממיר HTML ל-PDF עם כותרות מותאמות אישית
        /// </summary>
        /// <param name="html">תוכן HTML</param>
        /// <param name="documentTitle">כותרת המסמך</param>
        /// <param name="headerHtml">HTML לכותרת עליונה</param>
        /// <param name="footerHtml">HTML לכותרת תחתונה</param>
        /// <returns>מערך בייטים של PDF</returns>
        public byte[] ConvertToPdfWithCustomHeaderFooter(
            string html, 
            string documentTitle, 
            string headerHtml, 
            string footerHtml)
        {
            var globalSettings = new GlobalSettings
            {
                ColorMode = ColorMode.Color,
                Orientation = Orientation.Portrait,
                PaperSize = PaperKind.A4,
                Margins = new MarginSettings { Top = 25, Bottom = 25, Left = 10, Right = 10 },
                DocumentTitle = documentTitle,
            };

            var objectSettings = new ObjectSettings
            {
                PagesCount = true,
                HtmlContent = html,
                WebSettings = { DefaultEncoding = "utf-8", EnableJavascript = false },
                HeaderSettings = 
                { 
                    FontSize = 9, 
                    HtmUrl = string.IsNullOrEmpty(headerHtml) ? null : GetHtmlFileFromContent(headerHtml),
                    Line = true,
                    Spacing = 2.812
                },
                FooterSettings = 
                { 
                    FontSize = 9, 
                    HtmUrl = string.IsNullOrEmpty(footerHtml) ? null : GetHtmlFileFromContent(footerHtml),
                    Line = true,
                    Spacing = 2.812
                }
            };

            var pdf = new HtmlToPdfDocument()
            {
                GlobalSettings = globalSettings,
                Objects = { objectSettings }
            };

            return _converter.Convert(pdf);
        }

        /// <summary>
        /// יוצר קובץ HTML זמני מתוכן
        /// </summary>
        /// <param name="htmlContent">תוכן HTML</param>
        /// <returns>נתיב לקובץ זמני</returns>
        private string GetHtmlFileFromContent(string htmlContent)
        {
            string tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.html");
            File.WriteAllText(tempFile, htmlContent, Encoding.UTF8);
            return tempFile;
        }

        /// <summary>
        /// חילוץ חלק הכותרת העליונה מהתבנית
        /// </summary>
        /// <param name="html">תוכן HTML מלא</param>
        /// <returns>HTML של הכותרת העליונה</returns>
        public string ExtractHeaderFragment(string html)
        {
            var headerMatch = System.Text.RegularExpressions.Regex.Match(html, 
                @"<div\s+class=""page-header""[^>]*>(.*?)</div>", 
                System.Text.RegularExpressions.RegexOptions.Singleline);

            if (headerMatch.Success)
            {
                return $"<!DOCTYPE html><html><head><meta charset=\"UTF-8\"><style>body {{ direction: rtl; }}</style></head><body>{headerMatch.Value}</body></html>";
            }

            return null;
        }

        /// <summary>
        /// חילוץ חלק הכותרת התחתונה מהתבנית
        /// </summary>
        /// <param name="html">תוכן HTML מלא</param>
        /// <returns>HTML של הכותרת התחתונה</returns>
        public string ExtractFooterFragment(string html)
        {
            var footerMatch = System.Text.RegularExpressions.Regex.Match(html, 
                @"<div\s+class=""page-footer""[^>]*>(.*?)</div>", 
                System.Text.RegularExpressions.RegexOptions.Singleline);

            if (footerMatch.Success)
            {
                return $"<!DOCTYPE html><html><head><meta charset=\"UTF-8\"><style>body {{ direction: rtl; }}</style></head><body>{footerMatch.Value}</body></html>";
            }

            return null;
        }
    }
}
