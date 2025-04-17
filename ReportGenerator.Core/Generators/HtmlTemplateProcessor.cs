using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using HandlebarsDotNet;
using ReportGenerator.Core.Errors;
using ReportGenerator.Core.Interfaces;

namespace ReportGenerator.Core.Generators
{
    /// <summary>
    /// מעבד תבניות HTML - אחראי על עיבוד תבניות והחלפת פלייסהולדרים בערכים אמיתיים
    /// </summary>
    public class HtmlTemplateProcessor : ITemplateProcessor
    {
        private readonly Dictionary<string, string> _columnMappings;
        private readonly IHandlebars _handlebars;
        private readonly IErrorManager _errorManager;

        /// <summary>
        /// יוצר מופע חדש של מעבד תבניות
        /// </summary>
        /// <param name="columnMappings">מילון המיפויים בין שמות עמודות באנגלית לעברית</param>
        /// <param name="errorManager">מנהל שגיאות מוזרק</param>
        public HtmlTemplateProcessor(IDataAccess dataAccess, IErrorManager errorManager)
        {
            _errorManager = errorManager ?? throw new ArgumentNullException(nameof(errorManager));

            // קבלת מיפויים דרך DataAccess במקום הזרקה ישירה של Dictionary
            _columnMappings = dataAccess?.GetDefaultColumnMappings()?.Result
                ?? new Dictionary<string, string>();

            // אתחול מנוע Handlebars
            _handlebars = Handlebars.Create();

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
        }
        public HtmlTemplateProcessor(Dictionary<string, string> columnMappings, IErrorManager errorManager)
        {
            _columnMappings = columnMappings ?? new Dictionary<string, string>();
            _errorManager = errorManager ?? throw new ArgumentNullException(nameof(errorManager));

            // אתחול מנוע Handlebars
            _handlebars = Handlebars.Create();

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
                    _errorManager.LogError(
                        ErrorCode.Template_Invalid_Format,
                        ErrorSeverity.Critical,
                        "תבנית HTML לא יכולה להיות ריקה");
                    throw new ArgumentException("Template cannot be null or empty");
                }

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

                return result;
            }
            catch (Exception ex) when (!(ex is ArgumentException))
            {
                _errorManager.LogError(
                    ErrorCode.Template_Processing_Failed,
                    ErrorSeverity.Critical,
                    "שגיאה בעיבוד תבנית HTML",
                    ex);
                throw new Exception("Error processing HTML template", ex);
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
                _errorManager.LogWarning(
                    ErrorCode.Template_Processing_Failed,
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
                _errorManager.LogWarning(
                    ErrorCode.Template_Processing_Failed,
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

                foreach (var entry in values)
                {
                    string placeholder = $"{{{{{entry.Key}}}}}";
                    string value = FormatValue(entry.Value);
                    template = template.Replace(placeholder, value);
                }

                // הוספת ערכים מובנים
                template = template.Replace("{{CurrentDate}}", DateTime.Now.ToString("dd/MM/yyyy"));
                template = template.Replace("{{CurrentTime}}", DateTime.Now.ToString("HH:mm:ss"));

                return template;
            }
            catch (Exception ex)
            {
                _errorManager.LogWarning(
                    ErrorCode.Template_Processing_Failed,
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
                // תבנית מורחבת לתנאי if-else בתחביר Handlebars
                string pattern = @"\{\{#if\s+([^\s]+)\s*==\s*(-?\d+)\}\}([\s\S]*?)\{\{else\}\}([\s\S]*?)\{\{/if\}\}";
                //Console.WriteLine($"pattern: {pattern}");

                var matches = Regex.Matches(html, pattern);
                // Console.WriteLine($"matches.Count: {matches.Count}");

                return Regex.Replace(html, pattern, match => {
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
                _errorManager.LogWarning(
                    ErrorCode.Template_Condition_Invalid,
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
                _errorManager.LogWarning(
                    ErrorCode.Template_Condition_Invalid,
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

                while (true)
                {
                    // מציאת הקטע הבא
                    int sectionStart = html.IndexOf(startTag, startIndex, StringComparison.OrdinalIgnoreCase);
                    if (sectionStart < 0) break;

                    int sectionEnd = html.IndexOf(endTag, sectionStart + startTag.Length, StringComparison.OrdinalIgnoreCase);
                    if (sectionEnd < 0) break;

                    // חילוץ הקטע
                    string section = html.Substring(sectionStart, sectionEnd - sectionStart + endTag.Length);

                    // בדיקה אם יש תנאים בקטע
                    if (section.Contains("{{#if") && section.Contains("{{else}}") && section.Contains("{{/if}}"))
                    {
                        // עיבוד התנאים בקטע
                        string processedSection = ProcessHandlebarsConditions(section, dataRow);

                        // החלפת הקטע המקורי בקטע המעובד
                        html = html.Substring(0, sectionStart) + processedSection + html.Substring(sectionEnd + endTag.Length);

                        // עדכון אינדקס ההתחלה לחיפוש הבא
                        startIndex = sectionStart + processedSection.Length;
                    }
                    else
                    {
                        // אם אין תנאים, המשך לקטע הבא
                        startIndex = sectionEnd + endTag.Length;
                    }
                }

                return html;
            }
            catch (Exception ex)
            {
                _errorManager.LogWarning(
                    ErrorCode.Template_Condition_Invalid,
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
                        return mappedName;
                }
                else
                {
                    // שדה מחושב - חיפוש לפי שם השדה ישירות
                    if (_columnMappings.TryGetValue(columnName, out string mappedName))
                        return mappedName;
                }

                // אם לא מצאנו מיפוי, נחזיר את השם המקורי
                return columnName;
            }
            catch (Exception ex)
            {
                _errorManager.LogWarning(
                    ErrorCode.Template_Missing_Placeholder,
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
                var headerMatches = Regex.Matches(template, @"\{\{HEADER:([^}]+)\}\}");

                foreach (Match match in headerMatches)
                {
                    string columnName = match.Groups[1].Value;
                    string hebrewHeader = GetHebrewName(columnName, null);

                    template = template.Replace(match.Value, hebrewHeader);
                }

                return template;
            }
            catch (Exception ex)
            {
                _errorManager.LogWarning(
                    ErrorCode.Template_Processing_Failed,
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

                // ביטוי רגולרי משופר שתומך גם ב-TR וגם ב-DIV
                var tableRowMatches = Regex.Matches(template,
                    @"<(tr|div)[^>]*data-table-row=""([^""]+)""[^>]*>(.*?)</\1>",
                    RegexOptions.Singleline);

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
                        _errorManager.LogWarning(
                            ErrorCode.Template_Table_Row_Missing,
                            $"לא נמצאו נתונים לטבלה: {tableName}");

                        string noDataRow = "<div>אין נתונים להצגה</div>";
                        template = template.Replace(match.Value, noDataRow);
                        continue;
                    }

                    var dataTable = dataTables[keyToUse];
                    StringBuilder rowsBuilder = new StringBuilder();

                    foreach (DataRow row in dataTable.Rows)
                    {
                        string currentRow = rowTemplate;

                        // עיבוד תנאים בתחביר Handlebars
                        currentRow = ProcessHandlebarsConditions(currentRow, row);

                        // החלפת פלייסהולדרים בערכים
                        foreach (DataColumn col in dataTable.Columns)
                        {
                            string placeholder = $"{{{{{col.ColumnName}}}}}";
                            string value = FormatValue(row[col]);

                            if (currentRow.Contains(placeholder))
                            {
                                currentRow = currentRow.Replace(placeholder, value);
                            }
                        }

                        if (tagName.ToLower() == "tr")
                            rowsBuilder.AppendLine($"<tr>{currentRow}</tr>");
                        else
                            rowsBuilder.Append($"<{tagName}>{currentRow}</{tagName}>");
                    }

                    template = template.Replace(match.Value, rowsBuilder.ToString());
                }

                return template;
            }
            catch (Exception ex)
            {
                _errorManager.LogError(
                    ErrorCode.Template_Table_Row_Invalid,
                    ErrorSeverity.Error,
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
                _errorManager.LogWarning(
                    ErrorCode.Template_Processing_Failed,
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
                _errorManager.LogWarning(
                    ErrorCode.Template_Table_Row_Missing,
                    $"שגיאה במציאת מפתח טבלה {requestedName}",
                    ex);
                return null;
            }
        }
    }
}