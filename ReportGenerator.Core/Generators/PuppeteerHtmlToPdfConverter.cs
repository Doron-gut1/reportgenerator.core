using System;
using System.IO;
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
                    WaitUntil = new[] { WaitUntilNavigation.NetworkIdle0 }
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
    }
}
