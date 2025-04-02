using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using PuppeteerSharp;
using PuppeteerSharp.Media;
using ReportGenerator.Core.Errors;

namespace ReportGenerator.Core.Generators
{
    /// <summary>
    /// ממיר HTML ל-PDF באמצעות PuppeteerSharp (Chrome Headless)
    /// </summary>
    public class PuppeteerHtmlToPdfConverter : IHtmlToPdfConverter
    {
        private readonly string _chromePath;
        private readonly bool _useHeadless = true;
        private readonly PdfOptions _defaultPdfOptions;

        /// <summary>
        /// יוצר מופע חדש של ממיר HTML ל-PDF
        /// </summary>
        /// <param name="chromePath">נתיב לתוכנת Chrome (אופציונלי)</param>
        public PuppeteerHtmlToPdfConverter(string chromePath = null)
        {
            _chromePath = chromePath;
            
            // יצירת הגדרות ברירת מחדל עבור PDF
            _defaultPdfOptions = new PdfOptions
            {
                Format = PaperFormat.A4,
                PrintBackground = true,
                MarginOptions = new MarginOptions
                {
                    Top = "10mm",
                    Right = "10mm",
                    Bottom = "10mm",
                    Left = "10mm"
                },
                PreferCSSPageSize = true
            };
        }

        /// <summary>
        /// המרת HTML לקובץ PDF
        /// </summary>
        /// <param name="html">תוכן ה-HTML להמרה</param>
        /// <param name="options">אפשרויות PDF (אופציונלי)</param>
        /// <returns>מערך בייטים של קובץ ה-PDF</returns>
        public async Task<byte[]> ConvertToPdfAsync(string html, object options = null)
        {
            if (string.IsNullOrEmpty(html))
            {
                ErrorManager.LogError(
                    ErrorCodes.PDF.Html_Conversion_Failed,
                    ErrorSeverity.Critical,
                    "תוכן HTML ריק או null נשלח להמרה");
                throw new ArgumentException("HTML content cannot be null or empty");
            }

            try
            {
                var browserOptions = new LaunchOptions
                {
                    Headless = _useHeadless
                };
                
                // אם נקבע נתיב ידני ל-Chrome, משתמשים בו
                if (!string.IsNullOrEmpty(_chromePath))
                {
                    browserOptions.ExecutablePath = _chromePath;
                }
                // אחרת מורידים את הגרסה הנדרשת אוטומטית
                else
                {
                    var browserFetcher = new BrowserFetcher();
                    await browserFetcher.DownloadAsync();
                    
                    ErrorManager.LogInfo(
                        "Chrome_Downloaded",
                        "Chrome Headless הורד והותקן בהצלחה לצורך המרת HTML ל-PDF");
                }
                
                // פתיחת דפדפן חדש
                using var browser = await Puppeteer.LaunchAsync(browserOptions);
                using var page = await browser.NewPageAsync();
                
                // הגדרת encoding ושפה
                await page.SetContentAsync(html, new NavigationOptions
                {
                    WaitUntil = new[] { WaitUntilNavigation.NetworkIdle }
                });
                
                // המרה ל-PDF עם הגדרות מותאמות או ברירת מחדל
                var pdfOptions = options as PdfOptions ?? _defaultPdfOptions;
                var pdfBytes = await page.PdfDataAsync(pdfOptions);
                
                // תיעוד הצלחה
                ErrorManager.LogInfo(
                    "PDF_Conversion_Success",
                    $"המרת HTML ל-PDF הושלמה בהצלחה. גודל: {pdfBytes.Length / 1024:N0} KB");
                
                return pdfBytes;
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                // שגיאת גישה לקובץ Chrome
                ErrorManager.LogError(
                    ErrorCodes.PDF.Chrome_Not_Found,
                    ErrorSeverity.Critical,
                    "שגיאה בהפעלת Chrome. וודא שהתוכנה מותקנת או שהנתיב תקין",
                    ex);
                throw new Exception("Failed to start Chrome browser. Make sure Chrome is installed or path is correct", ex);
            }
            catch (Exception ex)
            {
                ErrorManager.LogError(
                    ErrorCodes.PDF.Html_Conversion_Failed,
                    ErrorSeverity.Critical,
                    "שגיאה בהמרת HTML ל-PDF",
                    ex);
                throw new Exception("Error converting HTML to PDF", ex);
            }
        }

        /// <summary>
        /// המרת HTML מקובץ לקובץ PDF
        /// </summary>
        /// <param name="htmlFilePath">נתיב לקובץ HTML</param>
        /// <param name="pdfFilePath">נתיב לקובץ PDF פלט</param>
        /// <returns>מערך בייטים של קובץ ה-PDF</returns>
        public async Task<byte[]> ConvertFileToPdfAsync(string htmlFilePath, string pdfFilePath = null)
        {
            try
            {
                if (!File.Exists(htmlFilePath))
                {
                    ErrorManager.LogError(
                        ErrorCodes.PDF.Html_Conversion_Failed,
                        ErrorSeverity.Critical,
                        $"קובץ HTML לא קיים: {htmlFilePath}");
                    throw new FileNotFoundException($"HTML file not found: {htmlFilePath}");
                }
                
                // קריאת תוכן הקובץ
                string htmlContent = await File.ReadAllTextAsync(htmlFilePath);
                
                // המרה ל-PDF
                byte[] pdfBytes = await ConvertToPdfAsync(htmlContent);
                
                // שמירה לקובץ אם נדרש
                if (!string.IsNullOrEmpty(pdfFilePath))
                {
                    await File.WriteAllBytesAsync(pdfFilePath, pdfBytes);
                    ErrorManager.LogInfo(
                        "PDF_File_Saved", 
                        $"קובץ PDF נשמר: {pdfFilePath}");
                }
                
                return pdfBytes;
            }
            catch (FileNotFoundException ex)
            {
                // כבר טופל למעלה
                throw;
            }
            catch (Exception ex)
            {
                ErrorManager.LogError(
                    ErrorCodes.PDF.Html_Conversion_Failed,
                    ErrorSeverity.Critical,
                    $"שגיאה בהמרת קובץ HTML ל-PDF: {htmlFilePath}",
                    ex);
                throw new Exception($"Error converting HTML file to PDF: {htmlFilePath}", ex);
            }
        }

        /// <summary>
        /// ממיר תוכן HTML ל-PDF (לתאימות עם הממשק המקורי)
        /// </summary>
        /// <param name="html">תוכן HTML</param>
        /// <param name="title">כותרת המסמך</param>
        /// <returns>מערך בייטים של PDF</returns>
        public async Task<byte[]> ConvertToPdf(string html, string title = null)
        {
            // אם יש כותרת, מוסיף אותה ל-HTML
            if (!string.IsNullOrEmpty(title))
            {
                html = html.Replace("<title></title>", $"<title>{title}</title>");
                html = html.Replace("<title>", $"<title>{title} - ");
            }

            return await ConvertToPdfAsync(html);
        }

        /// <summary>
        /// ממיר HTML ל-PDF עם כותרות מותאמות (לתאימות עם הממשק המקורי)
        /// </summary>
        /// <param name="html">תוכן HTML</param>
        /// <param name="title">כותרת המסמך</param>
        /// <param name="headerHtml">HTML לכותרת עליונה</param>
        /// <param name="footerHtml">HTML לכותרת תחתונה</param>
        /// <returns>מערך בייטים של PDF</returns>
        public async Task<byte[]> ConvertToPdfWithCustomHeaderFooter(string html, string title, string headerHtml, string footerHtml)
        {
            try
            {
                // שילוב הכותרות העליונה והתחתונה ב-HTML
                string fullHtml = html;
                
                // הוספת כותרת עליונה אם יש
                if (!string.IsNullOrEmpty(headerHtml))
                {
                    fullHtml = fullHtml.Replace("<body>", $"<body><div class=\"page-header\">{headerHtml}</div>");
                }
                
                // הוספת כותרת תחתונה אם יש
                if (!string.IsNullOrEmpty(footerHtml))
                {
                    fullHtml = fullHtml.Replace("</body>", $"<div class=\"page-footer\">{footerHtml}</div></body>");
                }
                
                // הוספת CSS לכותרות החוזרות
                string cssForHeaderFooter = @"
                .page-header {
                    position: running(header);
                    top: 0;
                }
                .page-footer {
                    position: running(footer);
                    bottom: 0;
                }
                @page {
                    @top-center { content: element(header) }
                    @bottom-center { content: element(footer) }
                }";
                
                fullHtml = fullHtml.Replace("</style>", cssForHeaderFooter + "</style>");
                
                // הוספת כותרת אם יש
                if (!string.IsNullOrEmpty(title))
                {
                    fullHtml = fullHtml.Replace("<title></title>", $"<title>{title}</title>");
                    fullHtml = fullHtml.Replace("<title>", $"<title>{title} - ");
                }
                
                return await ConvertToPdfAsync(fullHtml);
            }
            catch (Exception ex)
            {
                ErrorManager.LogError(
                    ErrorCodes.PDF.Html_Conversion_Failed,
                    ErrorSeverity.Error,
                    "שגיאה בהמרת HTML עם כותרות מותאמות ל-PDF",
                    ex);
                    
                // אם נכשל, ננסה להמיר ללא כותרות מותאמות
                return await ConvertToPdf(html, title);
            }
        }

        /// <summary>
        /// חילוץ חלק הכותרת העליונה מתבנית HTML
        /// </summary>
        /// <param name="html">תוכן HTML</param>
        /// <returns>HTML של הכותרת העליונה</returns>
        public string ExtractHeaderFragment(string html)
        {
            try
            {
                // חיפוש תגית header או div עם class שמכיל header
                var headerMatch = Regex.Match(html, 
                    @"<header.*?>(.*?)</header>|<div\s+class=(['""]).*?header.*?\2.*?>(.*?)</div>", 
                    RegexOptions.Singleline);
                
                if (headerMatch.Success)
                {
                    // החזרת התוכן שנמצא
                    return headerMatch.Groups[1].Success 
                        ? headerMatch.Groups[1].Value 
                        : headerMatch.Groups[3].Value;
                }
                
                // אם לא נמצא, מחזיר כותרת ברירת מחדל
                return "<div style='text-align:center; font-size:10pt;'></div>";
            }
            catch (Exception ex)
            {
                ErrorManager.LogWarning(
                    ErrorCodes.PDF.Html_Conversion_Failed,
                    "שגיאה בחילוץ כותרת עליונה מ-HTML",
                    ex);
                    
                return "<div style='text-align:center; font-size:10pt;'></div>";
            }
        }

        /// <summary>
        /// חילוץ חלק הכותרת התחתונה מתבנית HTML
        /// </summary>
        /// <param name="html">תוכן HTML</param>
        /// <returns>HTML של הכותרת התחתונה</returns>
        public string ExtractFooterFragment(string html)
        {
            try
            {
                // חיפוש תגית footer או div עם class שמכיל footer
                var footerMatch = Regex.Match(html, 
                    @"<footer.*?>(.*?)</footer>|<div\s+class=(['""]).*?footer.*?\2.*?>(.*?)</div>", 
                    RegexOptions.Singleline);
                
                if (footerMatch.Success)
                {
                    // החזרת התוכן שנמצא
                    return footerMatch.Groups[1].Success 
                        ? footerMatch.Groups[1].Value 
                        : footerMatch.Groups[3].Value;
                }
                
                // אם לא נמצא, מחזיר כותרת תחתונה ברירת מחדל עם מספרי עמודים
                return "<div style='text-align:center; font-size:10pt;'>עמוד <span class='pageNumber'></span> מתוך <span class='totalPages'></span></div>";
            }
            catch (Exception ex)
            {
                ErrorManager.LogWarning(
                    ErrorCodes.PDF.Html_Conversion_Failed,
                    "שגיאה בחילוץ כותרת תחתונה מ-HTML",
                    ex);
                    
                return "<div style='text-align:center; font-size:10pt;'>עמוד <span class='pageNumber'></span> מתוך <span class='totalPages'></span></div>";
            }
        }
    }
}
