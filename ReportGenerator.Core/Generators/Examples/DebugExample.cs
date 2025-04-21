using System;
using System.IO;
using System.Threading.Tasks;

namespace ReportGenerator.Core.Generators.Examples
{
    /// <summary>
    /// כלי דיבוג לבדיקת תבניות HTML
    /// </summary>
    public static class DebugExample
    {
        /// <summary>
        /// בדיקת תבנית ספציפית
        /// </summary>
        public static async Task<string> AnalyzeTemplate(string templatePath)
        {
            try
            {
                var issues = await TemplateDebugHelper.CheckTemplateFile(templatePath, false);
                
                var issuesDict = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<TemplateIssue>>();
                issuesDict.Add(Path.GetFileName(templatePath), issues);
                
                return TemplateDebugHelper.FormatIssuesReport(issuesDict);
            }
            catch (Exception ex)
            {
                return $"שגיאה בבדיקת התבנית: {ex.Message}";
            }
        }
        
        /// <summary>
        /// בדיקת כל תבניות HTML בתיקייה וייצור דוח
        /// </summary>
        public static async Task<string> AnalyzeAllTemplates(string folderPath, bool autoFix = false)
        {
            try
            {
                var results = await TemplateDebugHelper.CheckAllTemplatesInFolder(folderPath, autoFix);
                return TemplateDebugHelper.FormatIssuesReport(results);
            }
            catch (Exception ex)
            {
                return $"שגיאה בבדיקת התבניות: {ex.Message}";
            }
        }
        
        /// <summary>
        /// כתיבת דוח לקובץ
        /// </summary>
        public static async Task WriteReportToFile(string report, string outputPath)
        {
            try
            {
                await File.WriteAllTextAsync(outputPath, report);
                Console.WriteLine($"הדוח נשמר בהצלחה ב: {outputPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"שגיאה בשמירת הדוח: {ex.Message}");
            }
        }
        
        /// <summary>
        /// דוגמת שימוש לבדיקה ותיקון דוח ספציפי
        /// </summary>
        public static async Task CheckAndFixSpecificReport()
        {
            string templatePath = @"C:\doron\TFS\gloabl projects\ReportGenerator.Core\ReportGenerator.Core\Generators\Examples\TrfbysugtsSummaryReport.html";
            
            // 1. בדיקה ללא תיקון אוטומטי
            string report = await AnalyzeTemplate(templatePath);
            Console.WriteLine(report);
            
            // 2. בדיקה נוספת עם תיקון אוטומטי
            await TemplateDebugHelper.CheckTemplateFile(templatePath, true);
            
            // 3. בדיקה שוב לאחר התיקון
            string reportAfterFix = await AnalyzeTemplate(templatePath);
            Console.WriteLine("\nלאחר תיקון:\n" + reportAfterFix);
        }
        
        /// <summary>
        /// דוגמת שימוש לבדיקת כל הדוחות בתיקייה
        /// </summary>
        public static async Task CheckAllTemplatesExample()
        {
            string folderPath = @"C:\doron\TFS\gloabl projects\ReportGenerator.Core\ReportGenerator.Core\Generators\Examples";
            
            // בדיקה ללא תיקון
            string report = await AnalyzeAllTemplates(folderPath, false);
            Console.WriteLine(report);
            
            // שמירת הדוח לקובץ
            await WriteReportToFile(report, Path.Combine(folderPath, "TemplateAnalysisReport.txt"));
        }
    }
}