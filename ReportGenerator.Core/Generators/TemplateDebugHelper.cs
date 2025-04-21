using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HandlebarsDotNet;

namespace ReportGenerator.Core.Generators
{
    /// <summary>
    /// כלי עזר לניפוי שגיאות בתבניות HTML
    /// </summary>
    public static class TemplateDebugHelper
    {
        /// <summary>
        /// בודק ומתקן תבנית Handlebars
        /// </summary>
        /// <param name="templateContent">תוכן התבנית</param>
        /// <returns>תבנית מתוקנת וליסט של בעיות שנמצאו</returns>
        public static (string FixedTemplate, List<TemplateIssue> Issues) AnalyzeAndFixTemplate(string templateContent)
        {
            var issues = new List<TemplateIssue>();
            string fixedTemplate = templateContent;

            try
            {
                // 1. בדיקת תנאים עם סימן שווה בודד
                var singleEqualMatches = Regex.Matches(
                    fixedTemplate, 
                    @"\{\{#if\s+([^=\s]+)\s+=\s+([^}]+)\}\}"
                );

                foreach (Match match in singleEqualMatches)
                {
                    string original = match.Value;
                    string fixed_value = original.Replace(" = ", " == ");
                    
                    fixedTemplate = fixedTemplate.Replace(original, fixed_value);
                    
                    int lineNumber = GetLineNumber(templateContent, match.Index);
                    issues.Add(new TemplateIssue(
                        lineNumber,
                        "שימוש בסימן שוויון יחיד במקום שניים", 
                        original,
                        fixed_value,
                        TemplateIssueType.Syntax
                    ));
                }

                // 2. בדיקת שימוש בהלפרים לא מוגדרים
                var helperMatches = Regex.Matches(
                    fixedTemplate, 
                    @"\{\{(?:#if\s+)?\(?(?:eq|eqIsSummary)([^}]+)\)?\}\}"
                );

                foreach (Match match in helperMatches)
                {
                    string original = match.Value;
                    string helperName = match.Groups[1].Value.Trim().Split(' ')[0];
                    
                    // החלפת eq לדוגמה
                    if (original.Contains("(eq ") || original.Contains("(eqIsSummary "))
                    {
                        string varName = helperName.Trim();
                        string valueStr = match.Groups[1].Value.Replace(varName, "").Trim();
                        
                        string fixed_value;
                        if (original.StartsWith("{{#if"))
                        {
                            fixed_value = $"{{{{#if {varName} == {valueStr}}}}}";
                        }
                        else
                        {
                            fixed_value = $"{{{{{varName} == {valueStr}}}}}";
                        }
                        
                        fixedTemplate = fixedTemplate.Replace(original, fixed_value);
                        
                        int lineNumber = GetLineNumber(templateContent, match.Index);
                        issues.Add(new TemplateIssue(
                            lineNumber,
                            $"שימוש בהלפר לא מוגדר: {(original.Contains("eqIsSummary") ? "eqIsSummary" : "eq")}", 
                            original,
                            fixed_value,
                            TemplateIssueType.UndefinedHelper
                        ));
                    }
                }

                // 3. בדיקת סוגריים מאוזנים
                int openCurlyBraces = 0;
                int closeCurlyBraces = 0;
                int openIf = 0;
                int closeIf = 0;

                foreach (Match match in Regex.Matches(fixedTemplate, @"\{\{"))
                {
                    openCurlyBraces++;
                }

                foreach (Match match in Regex.Matches(fixedTemplate, @"\}\}"))
                {
                    closeCurlyBraces++;
                }

                foreach (Match match in Regex.Matches(fixedTemplate, @"\{\{#if"))
                {
                    openIf++;
                }

                foreach (Match match in Regex.Matches(fixedTemplate, @"\{\{/if\}\}"))
                {
                    closeIf++;
                }

                if (openCurlyBraces != closeCurlyBraces)
                {
                    issues.Add(new TemplateIssue(
                        0,
                        "סוגריים מסולסלים לא מאוזנים", 
                        $"נמצאו {openCurlyBraces} פתוחים ו-{closeCurlyBraces} סגורים",
                        "יש לוודא שלכל סוגר יש סוגר מתאים",
                        TemplateIssueType.BracesBalance
                    ));
                }

                if (openIf != closeIf)
                {
                    issues.Add(new TemplateIssue(
                        0,
                        "תגיות if לא מאוזנות", 
                        $"נמצאו {openIf} פתוחים ו-{closeIf} סגורים",
                        "יש לוודא שלכל if יש סוגר מתאים",
                        TemplateIssueType.TagsBalance
                    ));
                }

                // 4. ניסיון קומפילציה של התבנית
                try
                {
                    var handlebars = Handlebars.Create();
                    handlebars.RegisterHelper("header", (writer, context, parameters) => { });
                    handlebars.RegisterHelper("eq", (writer, options, context, arguments) => { });
                    handlebars.RegisterHelper("notEqualZero", (writer, options, context, arguments) => { });
                    handlebars.RegisterHelper("isSummary", (writer, options, context, arguments) => { });
                    handlebars.RegisterHelper("eqIsSummary", (writer, options, context, arguments) => { });
                    
                    var template = handlebars.Compile(fixedTemplate);
                }
                catch (Exception ex)
                {
                    // הוצאת מספר השורה והתו מתוך הודעת השגיאה
                    var lineErrorMatch = Regex.Match(ex.Message, @"Occured at: (\d+):(\d+)");
                    int lineNumber = 0;
                    int charPosition = 0;
                    
                    if (lineErrorMatch.Success)
                    {
                        int.TryParse(lineErrorMatch.Groups[1].Value, out lineNumber);
                        int.TryParse(lineErrorMatch.Groups[2].Value, out charPosition);
                    }
                    
                    // קבלת השורה הבעייתית
                    string lineContent = string.Empty;
                    if (lineNumber > 0)
                    {
                        string[] lines = fixedTemplate.Split('\n');
                        if (lines.Length >= lineNumber)
                        {
                            lineContent = lines[lineNumber - 1];
                        }
                    }
                    
                    issues.Add(new TemplateIssue(
                        lineNumber,
                        "שגיאת קומפילציה", 
                        lineContent,
                        ex.Message,
                        TemplateIssueType.CompilationError
                    ));
                }
            }
            catch (Exception ex)
            {
                issues.Add(new TemplateIssue(
                    0,
                    "שגיאה כללית בבדיקת התבנית", 
                    ex.Message,
                    "יש לבדוק את התבנית ידנית",
                    TemplateIssueType.General
                ));
            }

            return (fixedTemplate, issues);
        }

        /// <summary>
        /// מאתר את מספר השורה לפי האינדקס בתוכן
        /// </summary>
        private static int GetLineNumber(string content, int index)
        {
            if (index < 0 || index >= content.Length)
                return 0;

            int lineCount = 1;
            for (int i = 0; i < index; i++)
            {
                if (content[i] == '\n')
                    lineCount++;
            }

            return lineCount;
        }
        
        /// <summary>
        /// בודק תבנית מקובץ ומשמור תיקונים אם נדרש
        /// </summary>
        public static async Task<List<TemplateIssue>> CheckTemplateFile(string filePath, bool autoFix = false)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    return new List<TemplateIssue> 
                    { 
                        new TemplateIssue(0, "קובץ לא נמצא", filePath, "", TemplateIssueType.FileNotFound) 
                    };
                }
                
                string content = await File.ReadAllTextAsync(filePath);
                
                var (fixedTemplate, issues) = AnalyzeAndFixTemplate(content);
                
                if (autoFix && issues.Count > 0 && content != fixedTemplate)
                {
                    string backupPath = filePath + ".bak";
                    await File.WriteAllTextAsync(backupPath, content);
                    await File.WriteAllTextAsync(filePath, fixedTemplate);
                    
                    issues.Add(new TemplateIssue(
                        0,
                        "תבנית תוקנה אוטומטית", 
                        "הקובץ המקורי נשמר כ-" + backupPath,
                        "התיקונים נשמרו לקובץ המקורי",
                        TemplateIssueType.AutoFixed
                    ));
                }
                
                return issues;
            }
            catch (Exception ex)
            {
                return new List<TemplateIssue> 
                { 
                    new TemplateIssue(
                        0, 
                        "שגיאה בבדיקת הקובץ", 
                        ex.Message, 
                        "בדוק את הקובץ ידנית", 
                        TemplateIssueType.General) 
                };
            }
        }
        
        /// <summary>
        /// בדיקת כל התבניות בתיקייה
        /// </summary>
        public static async Task<Dictionary<string, List<TemplateIssue>>> CheckAllTemplatesInFolder(
            string folderPath, 
            bool autoFix = false,
            string searchPattern = "*.html")
        {
            var results = new Dictionary<string, List<TemplateIssue>>();
            
            try
            {
                if (!Directory.Exists(folderPath))
                {
                    results.Add("FOLDER_ERROR", new List<TemplateIssue>
                    {
                        new TemplateIssue(
                            0, 
                            "תיקייה לא נמצאה", 
                            folderPath, 
                            "", 
                            TemplateIssueType.FolderNotFound)
                    });
                    
                    return results;
                }
                
                string[] templateFiles = Directory.GetFiles(folderPath, searchPattern);
                
                foreach (string file in templateFiles)
                {
                    string fileName = Path.GetFileName(file);
                    var issues = await CheckTemplateFile(file, autoFix);
                    
                    results.Add(fileName, issues);
                }
                
                return results;
            }
            catch (Exception ex)
            {
                results.Add("GENERAL_ERROR", new List<TemplateIssue>
                {
                    new TemplateIssue(
                        0, 
                        "שגיאה כללית", 
                        ex.Message, 
                        "", 
                        TemplateIssueType.General)
                });
                
                return results;
            }
        }
        
        /// <summary>
        /// כלי עזר לתצוגה מסודרת של בעיות
        /// </summary>
        public static string FormatIssuesReport(Dictionary<string, List<TemplateIssue>> allIssues)
        {
            var sb = new System.Text.StringBuilder();
            
            sb.AppendLine("== דוח בדיקת תבניות HTML ==");
            sb.AppendLine();
            
            int totalIssues = 0;
            
            foreach (var fileEntry in allIssues)
            {
                string fileName = fileEntry.Key;
                var issues = fileEntry.Value;
                
                totalIssues += issues.Count;
                
                sb.AppendLine($"=== קובץ: {fileName} ===");
                
                if (issues.Count == 0)
                {
                    sb.AppendLine("✓ לא נמצאו בעיות");
                }
                else
                {
                    sb.AppendLine($"נמצאו {issues.Count} בעיות:");
                    
                    foreach (var issue in issues)
                    {
                        sb.AppendLine($"• שורה {issue.LineNumber}: {issue.Description}");
                        
                        if (!string.IsNullOrEmpty(issue.OriginalContent))
                        {
                            sb.AppendLine($"  מקור: {issue.OriginalContent}");
                        }
                        
                        if (!string.IsNullOrEmpty(issue.FixedContent))
                        {
                            sb.AppendLine($"  תיקון: {issue.FixedContent}");
                        }
                        
                        sb.AppendLine();
                    }
                }
                
                sb.AppendLine();
            }
            
            sb.AppendLine($"== סיכום: {totalIssues} בעיות ב-{allIssues.Count} קבצים ==");
            
            return sb.ToString();
        }
    }
    
    /// <summary>
    /// מחלקה המתארת בעיה שנמצאה בתבנית
    /// </summary>
    public class TemplateIssue
    {
        /// <summary>
        /// מספר השורה שבה נמצאה הבעיה (0 אם לא רלוונטי)
        /// </summary>
        public int LineNumber { get; }
        
        /// <summary>
        /// תיאור הבעיה
        /// </summary>
        public string Description { get; }
        
        /// <summary>
        /// התוכן המקורי שיש בו בעיה
        /// </summary>
        public string OriginalContent { get; }
        
        /// <summary>
        /// התוכן המתוקן (אם רלוונטי)
        /// </summary>
        public string FixedContent { get; }
        
        /// <summary>
        /// סוג הבעיה
        /// </summary>
        public TemplateIssueType IssueType { get; }
        
        public TemplateIssue(
            int lineNumber,
            string description,
            string originalContent,
            string fixedContent,
            TemplateIssueType issueType)
        {
            LineNumber = lineNumber;
            Description = description;
            OriginalContent = originalContent;
            FixedContent = fixedContent;
            IssueType = issueType;
        }
    }
    
    /// <summary>
    /// סוגי בעיות אפשריים בתבניות
    /// </summary>
    public enum TemplateIssueType
    {
        Syntax,             // בעיית תחביר
        UndefinedHelper,    // הלפר לא מוגדר
        BracesBalance,      // סוגריים לא מאוזנים
        TagsBalance,        // תגיות לא מאוזנות
        CompilationError,   // שגיאת קומפילציה
        FileNotFound,       // קובץ לא נמצא
        FolderNotFound,     // תיקייה לא נמצאה
        AutoFixed,          // תיקון אוטומטי בוצע
        General             // שגיאה כללית
    }
}