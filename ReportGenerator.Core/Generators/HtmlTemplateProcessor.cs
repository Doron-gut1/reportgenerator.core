using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using HandlebarsDotNet;

namespace ReportGenerator.Core.Generators
{
    /// <summary>
    /// מעבד תבניות HTML - אחראי על עיבוד תבניות והחלפת פלייסהולדרים בערכים אמיתיים
    /// </summary>
    public class HtmlTemplateProcessor
    {
        private readonly Dictionary<string, string> _columnMappings;
        private readonly IHandlebars _handlebars;
        /// <summary>
        /// יוצר מופע חדש של מעבד תבניות
        /// </summary>
        /// <param name="columnMappings">מילון המיפויים בין שמות עמודות באנגלית לעברית</param>
        public HtmlTemplateProcessor(Dictionary<string, string> columnMappings)
        {
            _columnMappings = columnMappings ?? new Dictionary<string, string>();

            // אתחול מנוע Handlebars
            _handlebars = Handlebars.Create();

            // רישום הלפר לפורמט מספרים
            _handlebars.RegisterHelper("format", (writer, context, parameters) => {
                if (parameters.Length > 0 && parameters[0] != null)
                {
                    writer.Write(FormatValue(parameters[0]));
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
            if (string.IsNullOrEmpty(template))
                throw new ArgumentException("Template cannot be null or empty");

            // 1. החלפת פלייסהולדרים פשוטים
            string result = ProcessSimplePlaceholders(template, values);

            // 2. החלפת כותרות בעברית
            result = ProcessHeaders(result);

            // 3. טיפול בטבלאות דינמיות - משתמשים בגרסה הפשוטה
            result = ProcessDynamicTables(result, dataTables);

            return result;
        }

        private string ProcessSimplePlaceholders(string template, Dictionary<string, object> values)
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

        private string ProcessSimpleConditions(string html, DataRow row)
        {
            // תבנית לתנאי שוויון: {{#if FieldName == Value}}...{{else}}...{{/if}}
            string pattern = @"\{\{#if\s+([^\s]+)\s*==\s*(\d+)\}\}(.*?)\{\{else\}\}(.*?)\{\{/if\}\}";

            return Regex.Replace(html, pattern, match => {
                string fieldName = match.Groups[1].Value;
                string valueStr = match.Groups[2].Value;
                string trueContent = match.Groups[3].Value;
                string falseContent = match.Groups[4].Value;

                // בדיקה אם השדה קיים בשורה
                if (row.Table.Columns.Contains(fieldName))
                {
                    // השוואת הערכים
                    var fieldValue = row[fieldName];

                    // המרת הערך לשורת השוואה
                    if (int.TryParse(valueStr, out int compareValue) &&
                        fieldValue != DBNull.Value)
                    {
                        // בדיקת שוויון
                        if (fieldValue is int intValue && intValue == compareValue)
                            return trueContent;

                        if (int.TryParse(fieldValue.ToString(), out int parsedValue) &&
                            parsedValue == compareValue)
                            return trueContent;
                    }
                }

                // אם לא מצאנו התאמה, החזר את החלק השלילי
                return falseContent;
            });
        }
        // פונקציה להחלפת תנאים בסיסיים בHTML (תנאי IsSummary)
      
        /// <summary>
        /// מקבל את השם העברי של עמודה לפי שם העמודה באנגלית
        /// מפעיל לוגיקת זיהוי ופיצול של שמות שדות
        /// </summary>
        /// <param name="columnName">שם העמודה באנגלית</param>
        /// <param name="procName">שם הפרוצדורה (אופציונלי)</param>
        /// <returns>הכותרת בעברית של העמודה</returns>
        private string GetHebrewName(string columnName, string procName)
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

        /// <summary>
        /// מחליף פלייסהולדרים של כותרות (HEADER:) בערכים עבריים מהמיפוי
        /// </summary>
        private string ProcessHeaders(string template)
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

        /// <summary>
        /// מעבד טבלאות דינמיות בתבנית - מחליף שורה אחת במספר שורות לפי הנתונים
        /// </summary>
        /// <summary>
        /// מעבד טבלאות דינמיות בתבנית - מחליף שורה אחת במספר שורות לפי הנתונים
        /// </summary>
        private string ProcessDynamicTables(string template, Dictionary<string, DataTable> dataTables)
        {
            if (dataTables == null || dataTables.Count == 0)
                return template;

            // ביטוי רגולרי משופר שתומך גם ב-TR וגם ב-DIV
            var tableRowMatches = Regex.Matches(template,
                @"<(tr|div)[^>]*data-table-row=""([^""]+)""[^>]*>(.*?)</\1>",
                RegexOptions.Singleline);

            foreach (Match match in tableRowMatches)
            {
                // שינוי: שם הטבלה עכשיו בקבוצה 2 במקום 1
                string tagName = match.Groups[1].Value;
                string tableName = match.Groups[2].Value;
                string rowTemplate = match.Groups[3].Value;

                // בדיקת מפתח עם התחשבות בקידומת dbo.
                var keyToUse = FindMatchingTableKey(dataTables, tableName);

                if (keyToUse == null)
                {
                    // אם אין נתונים, להציג הודעה
                    Console.WriteLine($"לא נמצאו נתונים לטבלה: {tableName}");
                    string noDataRow = "<div>אין נתונים להצגה</div>";
                    template = template.Replace(match.Value, noDataRow);
                    continue;
                }

                var dataTable = dataTables[keyToUse];
                StringBuilder rowsBuilder = new StringBuilder();

                foreach (DataRow row in dataTable.Rows)
                {
                    string currentRow = rowTemplate;

                    // עיבוד תנאים פשוטים - הוספת הקריאה לפונקציה החדשה
                    currentRow = ProcessSimpleConditions(currentRow, row);

                    // החלפת פלייסהולדרים בערכים
                    foreach (DataColumn col in dataTable.Columns)
                    {
                        string placeholder = $"{{{{{col.ColumnName}}}}}";
                        string value = FormatValue(row[col]);

                        // בדיקה אם הפלייסהולדר קיים בתבנית
                        if (currentRow.Contains(placeholder))
                        {
                            currentRow = currentRow.Replace(placeholder, value);
                        }
                    }
                    
                    // הסרת <tr> כי אנחנו מוסיפים שורה עם תג tr
                    if (tagName.ToLower() == "tr")
                        rowsBuilder.AppendLine($"<tr>{currentRow}</tr>");
                    else
                        rowsBuilder.Append($"<{tagName}>{currentRow}</{tagName}>");
                }

                template = template.Replace(match.Value, rowsBuilder.ToString());
            }

            return template;
        }

        /// <summary>
        /// פורמט ערכים לתצוגה
        /// </summary>
        private string FormatValue(object value)
        {
            if (value == null || value == DBNull.Value)
                return string.Empty;

            // אם הערך הוא מחרוזת, בדוק אם זו מחרוזת שמייצגת מספר
            if (value is string stringValue)
            {
                // נסה להמיר את המחרוזת למספר עשרוני
                if (decimal.TryParse(stringValue, out decimal parsedDecimal))
                {
                    // בדוק אם המספר הוא למעשה שלם
                    if (parsedDecimal == Math.Floor(parsedDecimal))
                    {
                        return parsedDecimal.ToString("#,##0", CultureInfo.InvariantCulture); // ללא נקודה עשרונית
                    }
                    else
                    {
                        return parsedDecimal.ToString("#,##0.00", CultureInfo.InvariantCulture); // עם נקודה עשרונית
                    }
                }
            }

            // אם הערך הוא מספר עשרוני
            if (value is decimal decimalValue)
            {
                // בדוק אם המספר הוא למעשה שלם
                if (decimalValue == Math.Floor(decimalValue))
                {
                    return decimalValue.ToString("#,##0", CultureInfo.InvariantCulture); // ללא נקודה עשרונית
                }
                else
                {
                    return decimalValue.ToString("#,##0.00", CultureInfo.InvariantCulture); // עם נקודה עשרונית
                }
            }

            if (value is double doubleValue)
            {
                // בדוק אם המספר הוא למעשה שלם
                if (doubleValue == Math.Floor(doubleValue))
                {
                    return doubleValue.ToString("#,##0", CultureInfo.InvariantCulture); // ללא נקודה עשרונית
                }
                else
                {
                    return doubleValue.ToString("#,##0.00", CultureInfo.InvariantCulture); // עם נקודה עשרונית
                }
            }

            if (value is float floatValue)
            {
                // בדוק אם המספר הוא למעשה שלם
                if (floatValue == Math.Floor(floatValue))
                {
                    return floatValue.ToString("#,##0", CultureInfo.InvariantCulture); // ללא נקודה עשרונית
                }
                else
                {
                    return floatValue.ToString("#,##0.00", CultureInfo.InvariantCulture); // עם נקודה עשרונית
                }
            }

            if (value is int intValue)
            {
                return intValue.ToString("#,##0", CultureInfo.InvariantCulture); // תמיד ללא נקודה עשרונית
            }

            // אם הערך הוא תאריך
            if (value is DateTime dateValue)
            {
                return dateValue.ToString("dd/MM/yyyy");
            }

            // אחרת, החזרת המחרוזת הרגילה
            return value.ToString();
        }


        /// <summary>
        /// מחפש מפתח מתאים במילון הנתונים, עם התחשבות בקידומת dbo.
        /// </summary>
        private string FindMatchingTableKey(Dictionary<string, DataTable> dataTables, string requestedName)
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
        private string ProcessSpecialCase(string html, DataRow row)
        {
            try
            {
                // בדיקה ספציפית ל-hesder = -1
                bool isTotal = false;

                if (row.Table.Columns.Contains("hesder") &&
                    row["hesder"] != DBNull.Value &&
                    int.TryParse(row["hesder"].ToString(), out int hesderVal))
                {
                    isTotal = (hesderVal == -1);
                }

                // החלפה ישירה של תגים לסיכום
                if (isTotal)
                {
                    // אם זה שורת סיכום, החלף את השורה בפורמט סיכום
                    html = html.Replace("{{hesder}}", "<strong>סה\"כ</strong>");

                    // הוסף דגש לכל הערכים
                    foreach (DataColumn col in row.Table.Columns)
                    {
                        string colName = col.ColumnName;
                        if (colName != "hesder")
                        {
                            string placeholder = $"{{{{{colName}}}}}";
                            string value = FormatValue(row[colName]);
                            html = html.Replace(placeholder, $"<strong>{value}</strong>");
                        }
                    }
                }

                return html;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in ProcessSpecialCase: {ex.Message}");
                return html;
            }
        }

        /// <summary>
        /// מעבד תנאים בתבנית HTML בצורה גנרית
        /// </summary>
        private string ProcessConditions(string html, DataRow row)
        {
            try
            {
                // יצירת מילון ערכים מהשורה
                var rowValues = new Dictionary<string, object>();
                foreach (DataColumn col in row.Table.Columns)
                {
                    rowValues[col.ColumnName] = row[col] == DBNull.Value ? null : row[col];
                }

                // ביטוי רגולרי לתפיסת תנאים בכל צורה אפשרית
                string ifPattern = @"\{\{#if\s+(.*?)\}\}(.*?)\{\{else\}\}(.*?)\{\{/if\}\}";

                return Regex.Replace(html, ifPattern, match => {
                    string condition = match.Groups[1].Value.Trim();
                    string trueContent = match.Groups[2].Value;
                    string falseContent = match.Groups[3].Value;

                    // הערכת התנאי באמצעות המעריך הגנרי
                    bool result = ExpressionEvaluator.Evaluate(condition, rowValues);

                    return result ? trueContent : falseContent;
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing conditions: {ex.Message}");
                return html;
            }
        }
    }
    /// <summary>
    /// מחלקה להערכת ביטויים תנאיים פשוטים
    /// </summary>
    public class ExpressionEvaluator
    {
        /// <summary>
        /// מעריך ביטוי תנאי בהתבסס על מילון ערכים
        /// </summary>
        public static bool Evaluate(string expression, Dictionary<string, object> values)
        {
            if (string.IsNullOrWhiteSpace(expression))
                return false;

            // ניתוח אופרטור שוויון
            if (expression.Contains("=="))
            {
                string[] parts = expression.Split(new[] { "==" }, StringSplitOptions.None);
                return CompareValues(parts[0].Trim(), parts[1].Trim(), values, (a, b) => Object.Equals(a, b));
            }

            // ניתוח אופרטור אי-שוויון
            if (expression.Contains("!="))
            {
                string[] parts = expression.Split(new[] { "!=" }, StringSplitOptions.None);
                return CompareValues(parts[0].Trim(), parts[1].Trim(), values, (a, b) => !Object.Equals(a, b));
            }

            // ניתוח אופרטור גדול מ
            if (expression.Contains(">"))
            {
                string[] parts = expression.Split(new[] { ">" }, StringSplitOptions.None);
                return CompareValues(parts[0].Trim(), parts[1].Trim(), values, (a, b) => {
                    if (a is IComparable c1 && b is IComparable)
                        return c1.CompareTo(b) > 0;
                    return false;
                });
            }

            // ניתוח אופרטור קטן מ
            if (expression.Contains("<"))
            {
                string[] parts = expression.Split(new[] { "<" }, StringSplitOptions.None);
                return CompareValues(parts[0].Trim(), parts[1].Trim(), values, (a, b) => {
                    if (a is IComparable c1 && b is IComparable)
                        return c1.CompareTo(b) < 0;
                    return false;
                });
            }

            // בדיקה של קיום פשוט (אם השדה קיים ולא null)
            if (values.TryGetValue(expression, out var value))
            {
                return value != null && value != DBNull.Value && !string.IsNullOrEmpty(value.ToString());
            }

            return false;
        }

        /// <summary>
        /// מבצע השוואה בין שני ערכים לפי אופרטור
        /// </summary>
        private static bool CompareValues(string left, string right, Dictionary<string, object> values,
                                        Func<object, object, bool> compareFunc)
        {
            object leftValue = ParseValue(left, values);
            object rightValue = ParseValue(right, values);

            // המרה של סוגי נתונים אם צריך
            if (leftValue != null && rightValue != null)
            {
                if (leftValue is string && rightValue is int)
                    leftValue = int.TryParse(leftValue.ToString(), out var intVal) ? intVal : leftValue;
                else if (leftValue is int && rightValue is string)
                    rightValue = int.TryParse(rightValue.ToString(), out var intVal) ? intVal : rightValue;
                else if (leftValue is string && rightValue is double)
                    leftValue = double.TryParse(leftValue.ToString(), out var doubleVal) ? doubleVal : leftValue;
                else if (leftValue is double && rightValue is string)
                    rightValue = double.TryParse(rightValue.ToString(), out var doubleVal) ? doubleVal : rightValue;
            }

            return compareFunc(leftValue, rightValue);
        }

        /// <summary>
        /// מנתח ערך מביטוי (קבוע או שם שדה)
        /// </summary>
        private static object ParseValue(string expression, Dictionary<string, object> values)
        {
            // בדיקה אם מדובר בקבוע מספרי
            if (int.TryParse(expression, out int intValue))
                return intValue;

            // בדיקה אם מדובר בקבוע עשרוני
            if (double.TryParse(expression, out double doubleValue))
                return doubleValue;

            // בדיקה אם מדובר במחרוזת
            if (expression.StartsWith("'") && expression.EndsWith("'"))
                return expression.Substring(1, expression.Length - 2);

            // אחרת, זהו שם שדה - קבלת הערך מהמילון
            if (values.TryGetValue(expression, out var value))
                return value;

            return null;
        }
    }
}