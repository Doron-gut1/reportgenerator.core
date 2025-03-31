using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using PuppeteerSharp;
using PuppeteerSharp.Media;

namespace ReportGenerator.Core.Generators
{
    /// <summary>
    /// ממיר HTML ל-PDF המבוסס על PuppeteerSharp (Chrome Headless)
    /// </summary>
    public class PuppeteerHtmlToPdfConverter : IHtmlToPdfConverter
    {
        private readonly string _chromePath;
        private readonly bool _downloadChrome;

        /// <summary>
        /// יוצר מופע חדש של ממיר HTML ל-PDF עם PuppeteerSharp
        /// </summary>
        /// <param name="chromePath">נתיב לקובץ ההפעלה של Chrome (אופציונלי)</param>
        /// <param name="downloadChrome">האם להוריד את Chrome אם לא קיים (ברירת מחדל: true)</param>
        public PuppeteerHtmlToPdfConverter(string chromePath = null, bool downloadChrome = true)
        {
            _chromePath = chromePath;
            _downloadChrome = downloadChrome;
        }

        /// <summary>
        /// ממיר HTML ל-PDF
        /// </summary>
        /// <param name="html">תוכן HTML</param>
        /// <param name="title">כותרת המסמך</param>
        /// <returns>מערך בייטים של PDF</returns>
        public async Task<byte[]> ConvertToPdf(string html, string title = null)
        {
            return await GetPdfContent(html, new PdfOptions
            {
                Format = PaperFormat.A4,
                PrintBackground = true,
                DisplayHeaderFooter = false,
                Margin = new MarginOptions
                {
                    Top = "1cm",
                    Bottom = "1cm",
                    Left = "1cm",
                    Right = "1cm"
                }
            });
        }

        /// <summary>
        /// ממיר HTML ל-PDF עם כותרות מותאמות
        /// </summary>
        /// <param name="html">תוכן HTML</param>
        /// <param name="title">כותרת המסמך</param>
        /// <param name="headerHtml">HTML לכותרת עליונה</param>
        /// <param name="footerHtml">HTML לכותרת תחתונה</param>
        /// <returns>מערך בייטים של PDF</returns>
        public async Task<byte[]> ConvertToPdfWithCustomHeaderFooter(string html, string title, string headerHtml, string footerHtml)
        {
            return await GetPdfContent(html, new PdfOptions
            {
                Format = PaperFormat.A4,
                PrintBackground = true,
                HeaderTemplate = headerHtml ?? "<div/>",
                FooterTemplate = footerHtml ?? "<div style=\"text-align: right;width: 297mm;font-size: 15px;\"><span style=\"margin-right: 1cm\">עמוד <span class=\"pageNumber\"></span> מתוך <span class=\"totalPages\"></span></span></div>",
                DisplayHeaderFooter = true,
                Margin = new MarginOptions
                {
                    Top = "2cm",
                    Bottom = "2cm",
                    Left = "1cm",
                    Right = "1cm"
                }
            });
        }

        /// <summary>
        /// חילוץ חלק הכותרת העליונה מתבנית HTML
        /// </summary>
        /// <param name="html">תוכן HTML</param>
        /// <returns>HTML של הכותרת העליונה</returns>
        public string ExtractHeaderFragment(string html)
        {
            var headerMatch = Regex.Match(html, 
                @"<div\s+class=""page-header""[^>]*>(.*?)</div>", 
                RegexOptions.Singleline);

            if (headerMatch.Success)
            {
                return $"<div style=\"text-align: center; width: 100%; font-family: Arial, sans-serif; direction: rtl;\">{headerMatch.Value}</div>";
            }

            return null;
        }

        /// <summary>
        /// חילוץ חלק הכותרת התחתונה מתבנית HTML
        /// </summary>
        /// <param name="html">תוכן HTML</param>
        /// <returns>HTML של הכותרת התחתונה</returns>
        public string ExtractFooterFragment(string html)
        {
            var footerMatch = Regex.Match(html, 
                @"<div\s+class=""page-footer""[^>]*>(.*?)</div>", 
                RegexOptions.Singleline);

            if (footerMatch.Success)
            {
                return $"<div style=\"text-align: center; width: 100%; font-family: Arial, sans-serif; direction: rtl;\">{footerMatch.Value}</div>";
            }

            return null;
        }

        /// <summary>
        /// יוצר PDF מתוכן HTML באמצעות Puppeteer
        /// </summary>
        private async Task<byte[]> GetPdfContent(string htmlString, PdfOptions options)
        {
            try
            {
                if (_downloadChrome)
                {
                    await new BrowserFetcher().DownloadAsync(BrowserFetcher.DefaultChromiumRevision);
                }

                await using (var browser = await Puppeteer.LaunchAsync(new LaunchOptions
                {
                    Headless = true,
                    ExecutablePath = !string.IsNullOrEmpty(_chromePath) ? _chromePath : null
                }))
                {
                    await using (var page = await browser.NewPageAsync())
                    {
                        await page.EmulateMediaTypeAsync(MediaType.Print);
                        await page.SetContentAsync(htmlString);
                        
                        var stream = await page.PdfStreamAsync(options);
                        using (var memoryStream = new MemoryStream())
                        {
                            byte[] buffer = new byte[16384];
                            int count;
                            
                            while ((count = stream.Read(buffer, 0, buffer.Length)) > 0)
                            {
                                memoryStream.Write(buffer, 0, count);
                            }
                            
                            return memoryStream.ToArray();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error generating PDF with PuppeteerSharp: {ex.Message}", ex);
            }
        }
    }
}