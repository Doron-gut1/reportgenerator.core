using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

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
                string value = entry.Value?.ToString() ?? string.Empty;
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
        private string ProcessDynamicTables(string template, Dictionary<string, DataTable> dataTables)
        {
            if (dataTables == null || dataTables.Count == 0)
                return template;
            
            var tableRowMatches = Regex.Matches(template, 
                @"<tr[^>]*data-table-row=""([^""]+)""[^>]*>(.*?)</tr>", 
                RegexOptions.Singleline);
            
            foreach (Match match in tableRowMatches)
            {
                string tableName = match.Groups[1].Value;
                string rowTemplate = match.Groups[2].Value;
                
                if (!dataTables.ContainsKey(tableName))
                {
                    // אם אין נתונים, להציג הודעה
                    string noDataRow = $"<tr><td colspan=\"100\">אין נתונים להצגה</td></tr>";
                    template = template.Replace(match.Value, noDataRow);
                    continue;
                }
                
                var dataTable = dataTables[tableName];
                StringBuilder rowsBuilder = new StringBuilder();
                
                foreach (DataRow row in dataTable.Rows)
                {
                    string currentRow = rowTemplate;
                    
                    foreach (DataColumn col in dataTable.Columns)
                    {
                        string placeholder = $"{{{{{col.ColumnName}}}}}";
                        string value = row[col]?.ToString() ?? string.Empty;
                        currentRow = currentRow.Replace(placeholder, value);
                    }
                    
                    rowsBuilder.AppendLine(currentRow);
                }
                
                template = template.Replace(match.Value, rowsBuilder.ToString());
            }
            
            return template;
        }
    }
}