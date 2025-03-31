using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ReportGenerator.Core.Generators
{
    /// <summary>
    /// מעבד תבניות HTML - אחראי על עיבוד תבניות והחלפת פלייסהולדרים בערכים אמיתיים
    /// </summary>
    public class HtmlTemplateProcessor
    {
        private readonly Dictionary<string, string> _columnMappings;

        /// <summary>
        /// יוצר מופע חדש של מעבד תבניות
        /// </summary>
        /// <param name="columnMappings">מילון המיפויים בין שמות עמודות באנגלית לעברית</param>
        public HtmlTemplateProcessor(Dictionary<string, string> columnMappings)
        {
            _columnMappings = columnMappings ?? new Dictionary<string, string>();
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

            // 3. טיפול בטבלאות דינמיות
            result = ProcessDynamicTables(result, dataTables);

            return result;
        }

        /// <summary>
        /// מחליף פלייסהולדרים פשוטים בערכיהם
        /// </summary>
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
                string tableName = match.Groups[2].Value;
                // שינוי: התוכן עכשיו בקבוצה 3 במקום 2
                string rowTemplate = match.Groups[3].Value;

                // לוג לדיבאג
               // Console.WriteLine($"נמצאה תבנית לטבלה: {tableName}, אורך התבנית: {rowTemplate.Length}");

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
                // לוג לדיבאג
                //Console.WriteLine($"נמצאה טבלת נתונים: {keyToUse}, מספר שורות: {dataTable.Rows.Count}, מספר עמודות: {dataTable.Columns.Count}");



                StringBuilder rowsBuilder = new StringBuilder();

                foreach (DataRow row in dataTable.Rows)
                {
                    string currentRow = rowTemplate;

                    foreach (DataColumn col in dataTable.Columns)
                    {
                        string placeholder = $"{{{{{col.ColumnName}}}}}";
                        string value = FormatValue(row[col]);

                        // בדיקה אם הפלייסהולדר קיים בתבנית
                        if (currentRow.Contains(placeholder))
                        {
                            currentRow = currentRow.Replace(placeholder, value);
                            //Console.WriteLine($"החלפת פלייסהולדר: {placeholder} בערך: {value}");
                        }
                    }

                    rowsBuilder.Append(currentRow);
                }

                // לוג לדיבאג - אורך התוצאה
                Console.WriteLine($"מספר תווים בתוצאה: {rowsBuilder.Length}");

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

            // אם הערך הוא מספר עשרוני
            if (value is decimal decimalValue)
                return decimalValue.ToString("0.00", CultureInfo.InvariantCulture);

            if (value is double doubleValue)
                return doubleValue.ToString("0.00", CultureInfo.InvariantCulture);

            if (value is float floatValue)
                return floatValue.ToString("0.00", CultureInfo.InvariantCulture);

            // אם הערך הוא תאריך
            if (value is DateTime dateValue)
                return dateValue.ToString("dd/MM/yyyy");

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
    }
}