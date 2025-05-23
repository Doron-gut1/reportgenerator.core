using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using PuppeteerSharp;
using PuppeteerSharp.Media;
using ReportGenerator.Core.Errors;
using ReportGenerator.Core.Interfaces;

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
        private readonly IErrorManager _errorManager;

        /// <summary>
        /// יוצר מופע חדש של ממיר HTML ל-PDF
        /// </summary>
        /// <param name="errorManager">מנהל שגיאות</param>
        /// <param name="chromePath">נתיב לתוכנת Chrome (אופציונלי)</param>
        public PuppeteerHtmlToPdfConverter(IErrorManager errorManager, string chromePath = @"C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe")
        {
            _errorManager = errorManager ?? throw new ArgumentNullException(nameof(errorManager));
            _chromePath = chromePath;

            // יצירת הגדרות ברירת מחדל עבור PDF
            _defaultPdfOptions = new PdfOptions
            {
                Format = PaperFormat.A4,
                PrintBackground = true,
                MarginOptions = new MarginOptions
                {
                    Top = "1cm",
                    Right = "1cm",
                    Bottom = "2cm", // שוליים תחתונים גדולים יותר עבור הפוטר
                    Left = "1cm"
                },
                DisplayHeaderFooter = true // אפשור כותרות עליונות ותחתונות בכל העמודים
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
                _errorManager.LogError(
                    ErrorCode.PDF_Html_Conversion_Failed,
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

                if (!string.IsNullOrEmpty(_chromePath))
                {
                    browserOptions.ExecutablePath = _chromePath;
                }
                else
                {
                    //var browserFetcher = new BrowserFetcher();
                    //await browserFetcher.DownloadAsync();

                    //_errorManager.LogInfo(
                    //    ErrorCode.General_Error,
                    //    "Chrome Headless הורד והותקן בהצלחה לצורך המרת HTML ל-PDF");
                    _errorManager.LogCriticalError(
                        ErrorCode.General_Error,
                        "there is no chrome in the classic path \"C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe");

                    throw new Exception($"Error generating report");
                }


                using var browser = await Puppeteer.LaunchAsync(browserOptions);
                using var page = await browser.NewPageAsync();

                await page.SetContentAsync(html, new NavigationOptions
                {
                    WaitUntil = new[] { WaitUntilNavigation.Networkidle0 }
                });

                var pdfOptions = options as PdfOptions ?? _defaultPdfOptions;
                var pdfBytes = await page.PdfDataAsync(pdfOptions);

                _errorManager.LogInfo(
                    ErrorCode.General_Error,
                    $"המרת HTML ל-PDF הושלמה בהצלחה. גודל: {pdfBytes.Length / 1024:N0} KB");

                return pdfBytes;
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                _errorManager.LogError(
                    ErrorCode.PDF_Chrome_Not_Found,
                    ErrorSeverity.Critical,
                    "שגיאה בהפעלת Chrome. וודא שהתוכנה מותקנת או שהנתיב תקין",
                    ex);
                throw new Exception("Failed to start Chrome browser. Make sure Chrome is installed or path is correct", ex);
            }
            catch (Exception ex)
            {
                _errorManager.LogError(
                    ErrorCode.PDF_Html_Conversion_Failed,
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
                    _errorManager.LogError(
                        ErrorCode.PDF_Html_Conversion_Failed,
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
                    _errorManager.LogInfo(
                        ErrorCode.General_Error,
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
                _errorManager.LogError(
                    ErrorCode.PDF_Html_Conversion_Failed,
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

            return await ConvertToPdfAsync(html, _defaultPdfOptions);
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
                var options = new PdfOptions
                {
                    Format = PaperFormat.A4,
                    PrintBackground = true,
                    HeaderTemplate = headerHtml ?? "<div/>",
                    FooterTemplate = footerHtml ?? GetDefaultFooterTemplate(),
                    DisplayHeaderFooter = true,
                    MarginOptions = new MarginOptions
                    {
                        Top = "2cm",
                        Bottom = "2cm",
                        Left = "1cm",
                        Right = "1cm"
                    }
                };

                if (!string.IsNullOrEmpty(title))
                {
                    html = html.Replace("<title></title>", $"<title>{title}</title>");
                    html = html.Replace("<title>", $"<title>{title} - ");
                }

                return await ConvertToPdfAsync(html, options);
            }
            catch (Exception ex)
            {
                _errorManager.LogError(
                    ErrorCode.PDF_Html_Conversion_Failed,
                    ErrorSeverity.Error,
                    "שגיאה בהמרת HTML עם כותרות מותאמות ל-PDF",
                    ex);

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
                    // החזרת התוכן שנמצא עם עיצוב
                    string content = headerMatch.Groups[1].Success
                        ? headerMatch.Groups[1].Value
                        : headerMatch.Groups[3].Value;

                    return $"<div style='text-align: center; width: 100%; font-family: Arial, sans-serif; direction: rtl;'>{content}</div>";
                }

                // אם לא נמצא, מחזיר כותרת ברירת מחדל
                return "<div style='text-align: center; font-size: 10pt;'></div>";
            }
            catch (Exception ex)
            {
                _errorManager.LogWarning(
                    ErrorCode.PDF_Html_Conversion_Failed,
                    "שגיאה בחילוץ כותרת עליונה מ-HTML",
                    ex);

                return "<div style='text-align: center; font-size: 10pt;'></div>";
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
                    // החזרת התוכן שנמצא עם עיצוב
                    string content = footerMatch.Groups[1].Success
                        ? footerMatch.Groups[1].Value
                        : footerMatch.Groups[3].Value;

                    return $"<div style='text-align: center; width: 100%; font-family: Arial, sans-serif; direction: rtl;'>{content}</div>";
                }

                // אם לא נמצא, מחזיר את תבנית הפוטר המורחבת הברירת מחדל
                return GetDefaultFooterTemplate();
            }
            catch (Exception ex)
            {
                _errorManager.LogWarning(
                    ErrorCode.PDF_Html_Conversion_Failed,
                    "שגיאה בחילוץ כותרת תחתונה מ-HTML",
                    ex);

                return GetDefaultFooterTemplate();
            }
        }

        /// <summary>
        /// יוצר תבנית פוטר ברירת מחדל עם מספור עמודים
        /// </summary>
        private string GetDefaultFooterTemplate()
        {
            return @"
            <div style='width: 100%; font-size: 10px; padding: 5px 10px; text-align: center; direction: rtl; font-family: Arial, sans-serif;'>
                <div>מסמך זה הופק באמצעות מערכת הדוחות החדשה</div>
                <div>עמוד <span class=""pageNumber""></span> מתוך <span class=""totalPages""></span></div>
            </div>";
        }
    }
}