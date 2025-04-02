using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ReportGenerator.Core.Errors;
using ReportGenerator.Core.Data.Models;

namespace ReportGenerator.Core.Generators
{
    /// <summary>
    /// מעבד תבניות HTML - אחראי על החלפת פלייסהולדרים בערכים אמיתיים
    /// </summary>
    public class HtmlTemplateProcessor
    {
        private readonly Dictionary<string, string> _columnMappings;

        /// <summary>
        /// יוצר מופע חדש של מעבד התבניות
        /// </summary>
        /// <param name="columnMappings">מילון מיפויים של שמות עמודות לשמות בעברית</param>
        public HtmlTemplateProcessor(Dictionary<string, string> columnMappings)
        {
            _columnMappings = columnMappings ?? new Dictionary<string, string>();
        }

        /// <summary>
        /// עיבוד תבנית HTML והחלפת פלייסהולדרים
        /// </summary>
        /// <param name="templateHtml">תוכן תבנית ה-HTML</param>
        /// <param name="reportTitle">כותרת הדוח</param>
        /// <param name="dataTables">מקורות הנתונים לדוח</param>
        /// <param name="parameters">פרמטרים נוספים להחלפה בתבנית</param>
        /// <returns>תוכן ה-HTML המלא לאחר החלפת כל הפלייסהולדרים</returns>
        public string ProcessTemplate(
            string templateHtml, 
            string reportTitle, 
            Dictionary<string, DataTable> dataTables, 
            Dictionary<string, ParamValue> parameters = null)
        {
            if (string.IsNullOrEmpty(templateHtml))
            {
                ErrorManager.LogError(
                    ErrorCodes.Template.Invalid_Format,
                    ErrorSeverity.Critical,
                    "תבנית HTML לא יכולה להיות ריקה");
                throw new ArgumentException("Template cannot be null or empty");
            }

            try
            {
                // 1. החלפת כותרת הדוח
                string result = templateHtml.Replace("{{ReportTitle}}", reportTitle);

                // 2. החלפת תאריך ושעה נוכחיים
                result = ReplaceCurrentDateAndTime(result);

                // 3. החלפת פרמטרים
                result = ReplaceParameters(result, parameters);

                // 4. החלפת כותרות בעברית
                result = ReplaceHebrewHeaders(result);

                // 5. עיבוד טבלאות דינמיות
                result = ProcessDynamicTables(result, dataTables);

                // 6. עיבוד תנאים
                result = ProcessConditionals(result);

                return result;
            }
            catch (Exception ex)
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
        /// החלפת תאריך ושעה נוכחיים
        /// </summary>
        private string ReplaceCurrentDateAndTime(string html)
        {
            var now = DateTime.Now;
            html = html.Replace("{{CurrentDate}}", now.ToString("dd/MM/yyyy"));
            html = html.Replace("{{CurrentTime}}", now.ToString("HH:mm:ss"));
            html = html.Replace("{{CurrentDateTime}}", now.ToString("dd/MM/yyyy HH:mm:ss"));
            return html;
        }

        /// <summary>
        /// החלפת פרמטרים משתנים
        /// </summary>
        private string ReplaceParameters(string html, Dictionary<string, ParamValue> parameters)
        {
            if (parameters == null || parameters.Count == 0)
                return html;

            foreach (var param in parameters)
            {
                string paramName = param.Key;
                string placeholder = $"{{{{{paramName}}}}}";
                
                if (html.Contains(placeholder))
                {
                    string value = FormatParameterValue(param.Value);
                    html = html.Replace(placeholder, value);
                }
            }

            return html;
        }

        /// <summary>
        /// פירמוט ערך פרמטר למחרוזת
        /// </summary>
        private string FormatParameterValue(ParamValue paramValue)
        {
            if (paramValue.Value == null)
                return string.Empty;

            if (paramValue.Value is DateTime dateValue)
                return dateValue.ToString("dd/MM/yyyy");

            if (paramValue.Value is decimal || paramValue.Value is double || paramValue.Value is float)
                return string.Format("{0:N2}", paramValue.Value);

            return paramValue.Value.ToString();
        }

        /// <summary>
        /// החלפת כותרות בעברית ({{HEADER:column}})
        /// </summary>
        private string ReplaceHebrewHeaders(string html)
        {
            try
            {
                var headerMatches = Regex.Matches(html, @"\{\{HEADER:([^}]+)\}\}");
                
                foreach (Match match in headerMatches)
                {
                    string columnName = match.Groups[1].Value;
                    
                    if (string.IsNullOrEmpty(columnName))
                    {
                        ErrorManager.LogWarning(
                            ErrorCodes.Template.Invalid_Placeholder,
                            $"נמצא פלייסהולדר HEADER ריק: {{{{HEADER:}}}}");
                        continue;
                    }

                    // ניסיון למצוא את השם העברי
                    string hebrewHeader = GetHebrewHeaderName(columnName);
                    
                    // החלפה בתבנית HTML
                    html = html.Replace(match.Value, hebrewHeader);
                }
                
                return html;
            }
            catch (Exception ex)
            {
                ErrorManager.LogNormalError(
                    ErrorCodes.Template.Processing_Failed,
                    "שגיאה בעיבוד כותרות בעברית",
                    ex);
                return html; // מחזיר את ה-HTML המקורי כדי לא לפגוע בתהליך
            }
        }

        /// <summary>
        /// קבלת שם עברי לעמודה
        /// </summary>
        private string GetHebrewHeaderName(string columnName)
        {
            try
            {
                // בדיקה אם יש מיפוי ישיר
                if (_columnMappings.TryGetValue(columnName, out string mappedName))
                    return mappedName;
                
                // בדיקה לפי קונבנציית TableName_ColumnName
                int underscoreIndex = columnName.IndexOf('_');
                if (underscoreIndex > 0 && underscoreIndex < columnName.Length - 1)
                {
                    // הפרדת שם הטבלה ושם העמודה
                    string tableName = columnName.Substring(0, underscoreIndex);
                    string fieldName = columnName.Substring(underscoreIndex + 1);
                    
                    // בדיקה אם יש מיפוי לשם המורכב
                    if (_columnMappings.TryGetValue(columnName, out mappedName))
                        return mappedName;
                }
                
                // אם לא נמצא מיפוי, מחזיר את השם המקורי
                return columnName;
            }
            catch (Exception ex)
            {
                ErrorManager.LogWarning(
                    ErrorCodes.Template.Missing_Placeholder,
                    $"שגיאה בקבלת שם עברי לעמודה {columnName}",
                    ex);
                return columnName; // במקרה של שגיאה, מחזיר את השם המקורי
            }
        }

        /// <summary>
        /// עיבוד טבלאות דינמיות (שורות עם data-table-row)
        /// </summary>
        private string ProcessDynamicTables(string html, Dictionary<string, DataTable> dataTables)
        {
            if (dataTables == null || dataTables.Count == 0)
                return html;

            try
            {
                // איתור כל השורות הדינמיות עם המאפיין data-table-row
                var tableRowMatches = Regex.Matches(html, 
                    @"<tr[^>]*data-table-row=""([^""]+)""[^>]*>(.*?)</tr>", 
                    RegexOptions.Singleline);
                
                foreach (Match match in tableRowMatches)
                {
                    string tableKey = match.Groups[1].Value;
                    string rowTemplate = match.Groups[0].Value; // כל תג ה-TR
                    string rowContent = match.Groups[2].Value;  // תוכן השורה בלבד
                    
                    // בדיקה אם יש נתונים לטבלה זו
                    if (!dataTables.TryGetValue(tableKey, out DataTable dataTable))
                    {
                        var errorMessage = $"לא נמצאו נתונים עבור טבלה דינמית {tableKey}";
                        ErrorManager.LogWarning(
                            ErrorCodes.Template.Table_Row_Missing,
                            errorMessage);
                        
                        // החלפה בהודעת שגיאה
                        string noDataRow = $"<tr><td colspan=\"100\" style=\"text-align:center;color:red;\">אין נתונים להצגה</td></tr>";
                        html = html.Replace(match.Value, noDataRow);
                        continue;
                    }
                    
                    // בניית שורות חדשות לפי הנתונים
                    StringBuilder rowsBuilder = new StringBuilder();
                    
                    if (dataTable.Rows.Count == 0)
                    {
                        // אם אין שורות בנתונים
                        string noDataRow = $"<tr><td colspan=\"100\" style=\"text-align:center;\">אין נתונים להצגה</td></tr>";
                        rowsBuilder.Append(noDataRow);
                    }
                    else
                    {
                        // עיבוד כל שורה בנתונים
                        foreach (DataRow row in dataTable.Rows)
                        {
                            string currentRow = rowContent;
                            
                            // החלפת כל פלייסהולדר בערך המתאים מהשורה
                            foreach (DataColumn col in dataTable.Columns)
                            {
                                string placeholder = $"{{{{{col.ColumnName}}}}}";
                                
                                if (currentRow.Contains(placeholder))
                                {
                                    string value = FormatDataCellValue(row[col]);
                                    currentRow = currentRow.Replace(placeholder, value);
                                }
                            }
                            
                            // בניית שורה מלאה עם תג TR מקורי אך תוכן מעודכן
                            string fullRow = rowTemplate.Replace(rowContent, currentRow);
                            rowsBuilder.Append(fullRow);
                        }
                    }
                    
                    // החלפת השורה המקורית בכל השורות שנבנו
                    html = html.Replace(match.Value, rowsBuilder.ToString());
                }
                
                return html;
            }
            catch (Exception ex)
            {
                ErrorManager.LogNormalError(
                    ErrorCodes.Template.Table_Row_Invalid,
                    "שגיאה בעיבוד טבלאות דינמיות",
                    ex);
                return html; // מחזיר את ה-HTML המקורי כדי לא לפגוע בתהליך
            }
        }

        /// <summary>
        /// פירמוט ערך תא בטבלה
        /// </summary>
        private string FormatDataCellValue(object value)
        {
            if (value == null || value == DBNull.Value)
                return string.Empty;

            if (value is DateTime dateValue)
                return dateValue.ToString("dd/MM/yyyy");

            if (value is decimal || value is double || value is float)
                return string.Format("{0:N2}", value);

            return value.ToString();
        }

        /// <summary>
        /// עיבוד תנאים ({{#if ... }} ... {{else}} ... {{/if}})
        /// </summary>
        private string ProcessConditionals(string html)
        {
            try
            {
                // איתור תנאי if-else-endif
                var conditionalMatches = Regex.Matches(html,
                    @"\{\{#if\s+([^}]+)\}\}(.*?)(?:\{\{else\}\}(.*?))?\{\{/if\}\}",
                    RegexOptions.Singleline);
                
                foreach (Match match in conditionalMatches)
                {
                    string condition = match.Groups[1].Value.Trim();
                    string ifContent = match.Groups[2].Value;
                    string elseContent = match.Groups[3].Success ? match.Groups[3].Value : string.Empty;
                    
                    bool conditionResult = EvaluateCondition(condition);
                    
                    // החלפת הביטוי התנאי בתוכן המתאים
                    html = html.Replace(match.Value, conditionResult ? ifContent : elseContent);
                }
                
                return html;
            }
            catch (Exception ex)
            {
                ErrorManager.LogNormalError(
                    ErrorCodes.Template.Condition_Invalid,
                    "שגיאה בעיבוד תנאים בתבנית",
                    ex);
                return html; // מחזיר את ה-HTML המקורי כדי לא לפגוע בתהליך
            }
        }

        /// <summary>
        /// הערכת ביטוי תנאי
        /// </summary>
        private bool EvaluateCondition(string condition)
        {
            // בינתיים תומך רק בתנאי שוויון פשוט: field == value
            var equalsMatch = Regex.Match(condition, @"(\w+)\s*==\s*([^\s]+)");
            
            if (equalsMatch.Success)
            {
                string field = equalsMatch.Groups[1].Value;
                string value = equalsMatch.Groups[2].Value;
                
                // החלפת מרכאות אם יש
                value = value.Trim('"', '\'');
                
                // בדיקה אם זה מספר שלילי
                if (value.StartsWith("-") && int.TryParse(value, out int intValue))
                {
                    // אם השדה הוא hesder והערך הוא -1, נחזיר אמת (לשורת סיכום)
                    if (field == "hesder" && intValue == -1)
                        return true;
                        
                    // אפשר להוסיף כאן בדיקות נוספות לפי הצורך
                }
                
                // הרחבה: אפשר להוסיף כאן הערכה של תנאים מורכבים יותר
            }
            
            // ברירת מחדל - אם לא הצלחנו להעריך את התנאי נחזיר שקר
            return false;
        }
    }
}
