using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using HandlebarsDotNet;
using ReportGenerator.Core.Errors;

namespace ReportGenerator.Core.Generators
{
    /// <summary>
    /// מעבד תבניות HTML - אחראי על עיבוד תבניות והחלפת פלייסהולדרים בערכים אמיתיים
    /// </summary>
    public class HtmlTemplateProcessor
    {
        private readonly Dictionary<string, string> _columnMappings;
        private readonly IHandlebars _handlebars;
        
        // מטמון עבור תבניות מקומפלות
        private readonly ConcurrentDictionary<string, HandlebarsTemplate<object, object>> _compiledTemplates = 
            new ConcurrentDictionary<string, HandlebarsTemplate<object, object>>();
            
        // מטמון עבור תבניות ביטויים רגולריים מקומפלים
        private readonly ConcurrentDictionary<string, Regex> _compiledRegexes = 
            new ConcurrentDictionary<string, Regex>();
            
        // מטמון עבור מיפויי כותרות
        private readonly ConcurrentDictionary<string, string> _headerMappingCache = 
            new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// יוצר מופע חדש של מעבד תבניות
        /// </summary>
        /// <param name="columnMappings">מילון המיפויים בין שמות עמודות באנגלית לעברית</param>
        public HtmlTemplateProcessor(Dictionary<string, string> columnMappings)
        {
            _columnMappings = columnMappings ?? new Dictionary<string, string>();

            // אתחול מנוע Handlebars
            _handlebars = Handlebars.Create(new HandlebarsConfiguration
            {
                NoEscape = true // אין צורך לברוח מתווים מיוחדים ב-HTML
            });

            // רישום הלפר לפורמט מספרים
            _handlebars.RegisterHelper("format", (writer, context, parameters) => {
                if (parameters.Length > 0 && parameters[0] != null)
                {
                    writer.Write(FormatValue(parameters[0]));
                }
            });

            _handlebars.RegisterHelper("notEqualZero", (writer, options, context, arguments) => {
                if (arguments.Length > 0 && arguments[0] != null && int.TryParse(arguments[0].ToString(), out int value))
                {
                    if (value != 0)
                    {
                        options.Template(writer, context);
                    }
                    else
                    {
                        options.Inverse(writer, context);
                    }
                }
            });
            
            // מקומפל מראש ביטויים רגולריים נפוצים
            CompileCommonRegexPatterns();
        }
        
        /// <summary>
        /// מקמפל מראש ביטויים רגולריים שכיחים לשיפור ביצועים
        /// </summary>
        private void CompileCommonRegexPatterns()
        {
            // ביטוי רגולרי לכותרות
            _compiledRegexes["header_pattern"] = new Regex(
                @"\{\{HEADER:([^}]+)\}\}",
                RegexOptions.Compiled);
                
            // ביטוי רגולרי לתנאים Handlebars
            _compiledRegexes["if_else_pattern"] = new Regex(
                @"\{\{#if\s+([^\s]+)\s*==\s*(-?\d+)\}\}([\s\S]*?)\{\{else\}\}([\s\S]*?)\{\{/if\}\}",
                RegexOptions.Compiled);
                
            // ביטוי רגולרי לטבלאות דינמיות
            _compiledRegexes["dynamic_table_pattern"] = new Regex(
                @"<(tr|div)[^>]*data-table-row=""([^""]+)""[^>]*>(.*?)</\1>",
                RegexOptions.Compiled | RegexOptions.Singleline);
        }

        /// <summary>
        /// מעבד תבנית HTML והחלפת כל הפלייסהולדרים בערכים
        /// </summary>
        /// <param name="template">תבנית HTML</param>
        /// <param name="values">מילון ערכים לפלייסהולדרים פשוטים</param>
        /// <param name="dataTables">טבלאות נתונים לפלייסהולדרים מורכבים</param>
        /// <returns>HTML מעובד עם ערכים אמיתיים</returns>
        public string ProcessTemplate(
                 string template,
                 Dictionary<string, object> values,
                 Dictionary<string, DataTable> dataTables)
        {
            try
            {
                if (string.IsNullOrEmpty(template))
                {
                    ErrorManager.LogError(
                        ErrorCodes.Template.Invalid_Format,
                        ErrorSeverity.Critical,
                        "תבנית HTML לא יכולה להיות ריקה");
                    throw new ArgumentException("Template cannot be null or empty");
                }

                // בדיקה אם התבנית כבר קיימת במטמון הקימפולים
                string templateKey = ComputeTemplateHash(template);
                if (_compiledTemplates.TryGetValue(templateKey, out var compiledTemplate))
                {
                    // נבדוק אם אפשר להשתמש בתבנית מקומפלת מראש
                    if (IsSimpleTemplate(template))
                    {
                        // אם זו תבנית פשוטה שמתאימה לשימוש ב-Handlebars (ללא טבלאות דינמיות)
                        // יצירת אובייקט עם הערכים הדרושים
                        var templateData = new Dictionary<string, object>(values);
                        
                        // הוספת ערכים מובנים
                        templateData["CurrentDate"] = DateTime.Now.ToString("dd/MM/yyyy");
                        templateData["CurrentTime"] = DateTime.Now.ToString("HH:mm:ss");
                        templateData["PageNumber"] = "<span class='pageNumber'></span>";
                        templateData["TotalPages"] = "<span class='totalPages'></span>";
                        
                        // הוספת כותרות עמודות מתורגמות
                        var headerMatches = _compiledRegexes["header_pattern"].Matches(template);
                        foreach (Match match in headerMatches)
                        {
                            string columnName = match.Groups[1].Value;
                            string headerKey = $"HEADER:{columnName}";
                            
                            // בדיקה אם הכותרת כבר קיימת במטמון
                            if (!templateData.ContainsKey(headerKey))
                            {
                                if (!_headerMappingCache.TryGetValue(columnName, out string hebrewHeader))
                                {
                                    hebrewHeader = GetHebrewName(columnName, null);
                                    _headerMappingCache[columnName] = hebrewHeader;
                                }
                                
                                templateData[headerKey] = hebrewHeader;
                            }
                        }
                        
                        // הרצת התבנית עם הנתונים
                        return compiledTemplate(templateData);
                    }
                }

                // הפעלת העיבוד הרגיל עבור תבניות מורכבות
                // 1. החלפת פלייסהולדרים פשוטים
                string result = ProcessSimplePlaceholders(template, values);

                // 2. החלפת כותרות בעברית
                result = ProcessHeaders(result);

                // 3. טיפול בטבלאות דינמיות עם תבניות
                result = ProcessDynamicTables(result, dataTables);

                // 4. טיפול בתנאים גלובליים (שלא בתוך טבלאות דינמיות)
                if (dataTables != null && dataTables.Count > 0)
                {
                    // מציאת "טבלת ברירת מחדל" - ניקח את הראשונה אם יש יותר מאחת
                    var defaultTable = GetDefaultDataTable(dataTables);

                    if (defaultTable != null && defaultTable.Rows.Count > 0)
                    {
                        // מחפשים שורת סיכום כברירת מחדל (בהנחה שהיא קיימת)
                        DataRow summaryRow = FindSummaryRow(defaultTable);

                        // שימוש בשורת הסיכום או בשורה הראשונה אם אין סיכום
                        DataRow row = summaryRow ?? defaultTable.Rows[0];

                        // עיבוד תנאים גלובליים בתבנית
                        result = ProcessGlobalConditions(result, row);
                    }
                }

                // 5. טיפול בפלייסהולדרים של מספור עמודים
                result = HandlePagePlaceholders(result);

                // אם זו תבנית חדשה, נשמור אותה במטמון אם היא מתאימה
                if (IsSimpleTemplate(template) && !_compiledTemplates.ContainsKey(templateKey))
                {
                    try
                    {
                        var compiled = _handlebars.Compile(template);
                        _compiledTemplates[templateKey] = compiled;
                    }
                    catch (Exception ex)
                    {
                        // אם יש שגיאה בקימפול, פשוט נמשיך בלי לשמור במטמון
                        ErrorManager.LogWarning(
                            "Template_Compilation_Failed",
                            $"לא ניתן לקמפל תבנית: {ex.Message}");
                    }
                }

                return result;
            }
            catch (Exception ex) when (!(ex is ArgumentException))
            {
                ErrorManager.LogError(
                    ErrorCodes.Template.Processing_Failed,
                    ErrorSeverity.Critical,
                    "שגיאה בעיבוד תבנית HTML",
                    ex);
                throw new Exception("Error processing HTML template", ex);
            }
        }
        
        /// <summary>
        /// בודק אם התבנית היא פשוטה מספיק כדי להשתמש ב-Handlebars
        /// </summary>
        private bool IsSimpleTemplate(string template)
        {
            // תבניות פשוטות הן תבניות ללא טבלאות דינמיות
            return !_compiledRegexes["dynamic_table_pattern"].IsMatch(template);
        }
        
        /// <summary>
        /// מייצר מפתח hash לתבנית לשימוש במטמון
        /// </summary>
        private string ComputeTemplateHash(string template)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(template));
                return Convert.ToBase64String(hashBytes);
            }
        }

        /// <summary>
        /// מחפש שורת סיכום לפי שדה hesder = -1
        /// </summary>
        private DataRow FindSummaryRow(DataTable table)
        {
            try
            {
                if (table.Columns.Contains("isSummary"))
                {
                    foreach (DataRow row in table.Rows)
                    {
                        if (row["isSummary"] != DBNull.Value)
                        {
                            if (row["isSummary"] is int intValue && (intValue == -1 || intValue == 1))
                                return row;

                            if (int.TryParse(row["isSummary"].ToString(), out int parsedValue) && parsedValue == 1)
                                return row;
                        }
                    }
                }
                return null;
            }
            catch (Exception ex)
            {
                ErrorManager.LogWarning(
                    ErrorCodes.Template.Processing_Failed,
                    "שגיאה בחיפוש שורת סיכום",
                    ex);
                return null;
            }
        }

        /// <summary>
        /// מקבל את טבלת ברירת המחדל לעיבוד תנאים גלובליים
        /// </summary>
        private DataTable GetDefaultDataTable(Dictionary<string, DataTable> dataTables)
        {
            try
            {
                // נסה למצוא את טבלת הנתונים של השלב החשוב ביותר
                string[] preferredTables = new[]{
                    "GetArnPaymentMethodSummary",
                    "dbo.GetArnPaymentMethodSummary",
                    "GetArnSummaryPeriodic",
                    "dbo.GetArnSummaryPeriodic"
                };

                foreach (var tableName in preferredTables)
                {
                    if (dataTables.ContainsKey(tableName) && dataTables[tableName].Rows.Count > 0)
                    {
                        return dataTables[tableName];
                    }
                }

                // אם לא מצאנו אחת מהטבלאות המועדפות, נחזיר את הראשונה
                return dataTables.Values.FirstOrDefault(t => t.Rows.Count > 0);
            }
            catch (Exception ex)
            {
                ErrorManager.LogWarning(
                    ErrorCodes.Template.Processing_Failed,
                    "שגיאה בהשגת טבלת ברירת מחדל",
                    ex);
                return null;
            }
        }

        private string ProcessSimplePlaceholders(string template, Dictionary<string, object> values)
        {
            try
            {
                if (values == null)
                    return template;
                
                // יוצרים חוצץ למהירות עבודה טובה יותר
                StringBuilder sb = new StringBuilder(template);

                foreach (var entry in values)
                {
                    string placeholder = $"{{{{{entry.Key}}}}}";
                    string value = FormatValue(entry.Value);
                    sb.Replace(placeholder, value);
                }

                // הוספת ערכים מובנים
                sb.Replace("{{CurrentDate}}", DateTime.Now.ToString("dd/MM/yyyy"));
                sb.Replace("{{CurrentTime}}", DateTime.Now.ToString("HH:mm:ss"));

                return sb.ToString();
            }
            catch (Exception ex)
            {
                ErrorManager.LogWarning(
                    ErrorCodes.Template.Processing_Failed,
                    "שגיאה בעיבוד פלייסהולדרים פשוטים",
                    ex);
                return template; // במקרה של שגיאה, החזרת התבנית המקורית
            }
        }

        /// <summary>
        /// מעבד תנאים בתחביר Handlebars בשורות של טבלאות
        /// </summary>
        private string ProcessHandlebarsConditions(string html, DataRow row)
        {
            try
            {
                // שימוש בביטוי רגולרי מקומפל
                var ifPattern = _compiledRegexes["if_else_pattern"];
                
                return ifPattern.Replace(html, match => {
                    string fieldName = match.Groups[1].Value;
                    string valueStr = match.Groups[2].Value;
                    string trueContent = match.Groups[3].Value;
                    string falseContent = match.Groups[4].Value;

                    // בדיקה אם השדה קיים בשורה
                    if (row.Table.Columns.Contains(fieldName))
                    {
                        // השוואת הערכים
                        object fieldValue = row[fieldName];
                        if (fieldValue == DBNull.Value)
                            return falseContent;

                        // המרת הערך לשורת השוואה
                        if (int.TryParse(valueStr, out int compareValue))
                        {
                            // טיפול במספרים שלמים
                            if (fieldValue is int intValue && intValue == compareValue)
                                return trueContent;

                            // ניסיון המרה של ערכים אחרים למספר שלם
                            if (int.TryParse(fieldValue.ToString(), out int parsedValue) &&
                                parsedValue == compareValue)
                                return trueContent;
                        }
                    }

                    // אם לא מצאנו התאמה, החזר את החלק השלילי
                    return falseContent;
                });
            }
            catch (Exception ex)
            {
                ErrorManager.LogWarning(
                    ErrorCodes.Template.Condition_Invalid,
                    $"שגיאה בעיבוד תנאים: {ex.Message}",
                    ex);
                return html; // מחזיר את ה-HTML המקורי במקרה של שגיאה
            }
        }

        /// <summary>
        /// מעבד תנאים גלובליים (לא בתוך טבלאות דינמיות)
        /// </summary>
        private string ProcessGlobalConditions(string html, DataRow dataRow)
        {
            try
            {
                // קטעים בעייתיים שמכילים תנאים בצורה לא תקינה
                var problematicSections = new List<(string start, string end)>
                {
                    ("<tbody>", "</tbody>"),
                    ("<tr>", "</tr>"),
                    ("<table", "</table>")
                };

                foreach (var section in problematicSections)
                {
                    html = ProcessConditionsInSection(html, section.start, section.end, dataRow);
                }

                return html;
            }
            catch (Exception ex)
            {
                ErrorManager.LogWarning(
                    ErrorCodes.Template.Condition_Invalid,
                    $"שגיאה בעיבוד תנאים גלובליים: {ex.Message}",
                    ex);
                return html;
            }
        }

        /// <summary>
        /// מעבד תנאים בקטע מסוים של ה-HTML
        /// </summary>
        private string ProcessConditionsInSection(string html, string startTag, string endTag, DataRow dataRow)
        {
            try
            {
                int startIndex = 0;
                
                // יצירת StringBuilder לביצועים טובים יותר
                StringBuilder result = new StringBuilder(html.Length);
                int lastAppendedIndex = 0;

                while (true)
                {
                    // מציאת הקטע הבא
                    int sectionStart = html.IndexOf(startTag, startIndex, StringComparison.OrdinalIgnoreCase);
                    if (sectionStart < 0) break;

                    int sectionEnd = html.IndexOf(endTag, sectionStart + startTag.Length, StringComparison.OrdinalIgnoreCase);
                    if (sectionEnd < 0) break;
                    
                    // הוסף את הקטע מהאינדקס האחרון שהוספנו עד תחילת הקטע הנוכחי
                    if (sectionStart > lastAppendedIndex)
                    {
                        result.Append(html, lastAppendedIndex, sectionStart - lastAppendedIndex);
                    }

                    // חילוץ הקטע
                    string sectionContent = html.Substring(sectionStart, sectionEnd - sectionStart + endTag.Length);

                    // בדיקה אם יש תנאים בקטע
                    if (sectionContent.Contains("{{#if") && sectionContent.Contains("{{else}}") && sectionContent.Contains("{{/if}}"))
                    {
                        // עיבוד התנאים בקטע
                        string processedSection = ProcessHandlebarsConditions(sectionContent, dataRow);
                        result.Append(processedSection);
                    }
                    else
                    {
                        // אם אין תנאים, הוסף את הקטע כמו שהוא
                        result.Append(sectionContent);
                    }
                    
                    // עדכון אינדקסים
                    lastAppendedIndex = sectionEnd + endTag.Length;
                    startIndex = lastAppendedIndex;
                }
                
                // הוסף את יתרת ה-HTML
                if (lastAppendedIndex < html.Length)
                {
                    result.Append(html, lastAppendedIndex, html.Length - lastAppendedIndex);
                }

                return result.ToString();
            }
            catch (Exception ex)
            {
                ErrorManager.LogWarning(
                    ErrorCodes.Template.Condition_Invalid,
                    $"שגיאה בעיבוד תנאים בקטע: {ex.Message}",
                    ex);
                return html; // במקרה של שגיאה, החזרת הקלט המקורי
            }
        }

        /// <summary>
        /// מקבל את השם העברי של עמודה לפי שם העמודה באנגלית
        /// מפעיל לוגיקת זיהוי ופיצול של שמות שדות
        /// </summary>
        /// <param name="columnName">שם העמודה באנגלית</param>
        /// <param name="procName">שם הפרוצדורה (אופציונלי)</param>
        /// <returns>הכותרת בעברית של העמודה</returns>
        private string GetHebrewName(string columnName, string procName)
        {
            try
            {
                // בדיקה במטמון קודם
                string cacheKey = $"{procName ?? ""}_{columnName}";
                if (_headerMappingCache.TryGetValue(cacheKey, out string cachedName))
                {
                    return cachedName;
                }
                
                string result;
                
                // בדיקה אם יש "_" בשם השדה
                int underscoreIndex = columnName.IndexOf('_');

                if (underscoreIndex > 0)
                {
                    // שדה מטבלה - פיצול לפי "_"
                    string tableName = columnName.Substring(0, underscoreIndex);
                    string fieldName = columnName.Substring(underscoreIndex + 1);

                    // חיפוש בטבלת המיפויים
                    string mappingKey = $"{tableName}_{fieldName}";
                    if (_columnMappings.TryGetValue(mappingKey, out string mappedName))
                        result = mappedName;
                    else
                        result = columnName;
                }
                else
                {
                    // שדה מחושב - חיפוש לפי שם השדה ישירות
                    if (_columnMappings.TryGetValue(columnName, out string mappedName))
                        result = mappedName;
                    else
                        result = columnName;
                }
                
                // הוספה למטמון
                _headerMappingCache[cacheKey] = result;
                
                return result;
            }
            catch (Exception ex)
            {
                ErrorManager.LogWarning(
                    ErrorCodes.Template.Missing_Placeholder,
                    $"שגיאה בקבלת שם עברי לעמודה {columnName}",
                    ex);
                return columnName; // במקרה של שגיאה, החזרת השם המקורי
            }
        }

        /// <summary>
        /// מחליף פלייסהולדרים של כותרות (HEADER:) בערכים עבריים מהמיפוי
        /// </summary>
        private string ProcessHeaders(string template)
        {
            try
            {
                var headerMatches = _compiledRegexes["header_pattern"].Matches(template);
                
                // אם אין התאמות, נחזיר את התבנית המקורית
                if (headerMatches.Count == 0)
                    return template;
                
                // משתמשים ב-StringBuilder לביצועים טובים יותר
                StringBuilder sb = new StringBuilder(template);

                foreach (Match match in headerMatches)
                {
                    string columnName = match.Groups[1].Value;
                    
                    // בדיקה במטמון
                    if (!_headerMappingCache.TryGetValue(columnName, out string hebrewHeader))
                    {
                        hebrewHeader = GetHebrewName(columnName, null);
                        _headerMappingCache[columnName] = hebrewHeader;
                    }

                    sb.Replace(match.Value, hebrewHeader);
                }

                return sb.ToString();
            }
            catch (Exception ex)
            {
                ErrorManager.LogWarning(
                    ErrorCodes.Template.Processing_Failed,
                    "שגיאה בעיבוד כותרות",
                    ex);
                return template; // במקרה של שגיאה, החזרת התבנית המקורית
            }
        }

        /// <summary>
        /// מעבד טבלאות דינמיות בתבנית - מחליף שורה אחת במספר שורות לפי הנתונים
        /// </summary>
        private string ProcessDynamicTables(string template, Dictionary<string, DataTable> dataTables)
        {
            try
            {
                if (dataTables == null || dataTables.Count == 0)
                    return template;

                // שימוש בביטוי רגולרי מקומפל
                var tableRowMatches = _compiledRegexes["dynamic_table_pattern"].Matches(template);
                
                // אם אין התאמות, נחזיר את התבנית המקורית
                if (tableRowMatches.Count == 0)
                    return template;
                
                // הכנת מבנה נתונים לשמירת ההחלפות
                Dictionary<string, string> replacements = new Dictionary<string, string>();

                foreach (Match match in tableRowMatches)
                {
                    string tagName = match.Groups[1].Value;
                    string tableName = match.Groups[2].Value;
                    string rowTemplate = match.Groups[3].Value;

                    // בדיקת מפתח עם התחשבות בקידומת dbo.
                    var keyToUse = FindMatchingTableKey(dataTables, tableName);

                    if (keyToUse == null)
                    {
                        // אם אין נתונים, להציג הודעה
                        ErrorManager.LogWarning(
                            ErrorCodes.Template.Table_Row_Missing,
                            $"לא נמצאו נתונים לטבלה: {tableName}");

                        string noDataRow = "<div>אין נתונים להצגה</div>";
                        replacements[match.Value] = noDataRow;
                        continue;
                    }

                    var dataTable = dataTables[keyToUse];
                    
                    // אומדן גודל החוצץ לפי מספר השורות וגודל התבנית
                    int estimatedSize = dataTable.Rows.Count * (rowTemplate.Length + 20);
                    StringBuilder rowsBuilder = new StringBuilder(estimatedSize);

                    foreach (DataRow row in dataTable.Rows)
                    {
                        string currentRow = rowTemplate;

                        // עיבוד תנאים בתחביר Handlebars
                        currentRow = ProcessHandlebarsConditions(currentRow, row);

                        // החלפת פלייסהולדרים בערכים - בניית מפתחות להחלפה מראש
                        Dictionary<string, string> rowReplacements = new Dictionary<string, string>();
                        foreach (DataColumn col in dataTable.Columns)
                        {
                            string placeholder = $"{{{{{col.ColumnName}}}}}";
                            
                            // רק אם הפלייסהולדר קיים בשורה, נבצע חישוב וניצור ערך להחלפה
                            if (currentRow.Contains(placeholder))
                            {
                                string value = FormatValue(row[col]);
                                rowReplacements[placeholder] = value;
                            }
                        }
                        
                        // ביצוע ההחלפות בפועל
                        foreach (var replacement in rowReplacements)
                        {
                            currentRow = currentRow.Replace(replacement.Key, replacement.Value);
                        }

                        if (tagName.ToLower() == "tr")
                            rowsBuilder.AppendLine($"<tr>{currentRow}</tr>");
                        else
                            rowsBuilder.AppendLine($"<{tagName}>{currentRow}</{tagName}>");
                    }

                    replacements[match.Value] = rowsBuilder.ToString();
                }
                
                // ביצוע כל ההחלפות בפעם אחת
                string result = template;
                foreach (var replacement in replacements)
                {
                    result = result.Replace(replacement.Key, replacement.Value);
                }

                return result;
            }
            catch (Exception ex)
            {
                ErrorManager.LogNormalError(
                    ErrorCodes.Template.Table_Row_Invalid,
                    "שגיאה בעיבוד טבלאות דינמיות",
                    ex);
                return template; // מחזיר את ה-HTML המקורי כדי לא לפגוע בתהליך
            }
        }

        /// <summary>
        /// מטפל בפלייסהולדרים של מספור עמודים
        /// </summary>
        private string HandlePagePlaceholders(string template)
        {
            try
            {
                // החלפת פלייסהולדרים של מספרי עמודים בתגיות מיוחדות של Puppeteer
                template = template.Replace("{{PageNumber}}", "<span class='pageNumber'></span>");
                template = template.Replace("{{TotalPages}}", "<span class='totalPages'></span>");

                return template;
            }
            catch (Exception ex)
            {
                ErrorManager.LogWarning(
                    ErrorCodes.Template.Processing_Failed,
                    "שגיאה בעיבוד פלייסהולדרים של מספרי עמודים",
                    ex);
                return template;
            }
        }

        /// <summary>
        /// פורמט ערכים לתצוגה
        /// </summary>
        public static string FormatValue(object value)
        {
            if (value == null || value == DBNull.Value)
                return string.Empty;

            var culture = CultureInfo.InvariantCulture;

            switch (value)
            {
                case string stringValue:
                    if (decimal.TryParse(stringValue, NumberStyles.Any, culture, out decimal parsedDecimal))
                    {
                        return parsedDecimal == Math.Floor(parsedDecimal)
                            ? parsedDecimal.ToString("#,##0", culture)
                            : parsedDecimal.ToString("#,##0.00", culture);
                    }
                    break;

                case decimal decimalValue:
                    return decimalValue == Math.Floor(decimalValue)
                        ? decimalValue.ToString("#,##0", culture)
                        : decimalValue.ToString("#,##0.00", culture);

                case double doubleValue:
                    return doubleValue == Math.Floor(doubleValue)
                        ? doubleValue.ToString("#,##0", culture)
                        : doubleValue.ToString("#,##0.00", culture);

                case float floatValue:
                    return floatValue == Math.Floor(floatValue)
                        ? floatValue.ToString("#,##0", culture)
                        : floatValue.ToString("#,##0.00", culture);

                case int intValue:
                    return intValue.ToString("#,##0", culture);

                case DateTime dateValue:
                    return dateValue.ToString("dd/MM/yyyy");

                default:
                    return value.ToString();
            }

            return value.ToString();
        }


        /// <summary>
        /// מחפש מפתח מתאים במילון הנתונים, עם התחשבות בקידומת dbo.
        /// </summary>
        private string FindMatchingTableKey(Dictionary<string, DataTable> dataTables, string requestedName)
        {
            try
            {
                // בדיקה ישירה
                if (dataTables.ContainsKey(requestedName))
                    return requestedName;

                // ניסיון עם קידומת dbo.
                string withPrefix = requestedName.StartsWith("dbo.") ? requestedName : "dbo." + requestedName;
                if (dataTables.ContainsKey(withPrefix))
                    return withPrefix;

                // ניסיון ללא קידומת dbo.
                if (requestedName.StartsWith("dbo."))
                {
                    string withoutPrefix = requestedName.Substring(4);
                    if (dataTables.ContainsKey(withoutPrefix))
                        return withoutPrefix;
                }

                // חיפוש מורחב - בדיקה עם סיומות שונות של השם
                foreach (var key in dataTables.Keys)
                {
                    // בדיקה של נקודה וסוגריים אחרי השם
                    if (key.StartsWith(requestedName + ".") || key.StartsWith(requestedName + "("))
                        return key;

                    // ניסיון עם קידומת
                    if (key.StartsWith("dbo." + requestedName))
                        return key;
                }

                // לא נמצא מפתח מתאים
                return null;
            }
            catch (Exception ex)
            {
                ErrorManager.LogWarning(
                    ErrorCodes.Template.Table_Row_Missing,
                    $"שגיאה במציאת מפתח טבלה {requestedName}",
                    ex);
                return null;
            }
        }
        
        /// <summary>
        /// ניקוי המטמון
        /// </summary>
        public void ClearCache()
        {
            _compiledTemplates.Clear();
            _headerMappingCache.Clear();
        }
    }
}