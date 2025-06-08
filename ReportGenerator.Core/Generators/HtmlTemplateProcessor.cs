using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using HandlebarsDotNet;
using ReportGenerator.Core.Errors;


namespace ReportGenerator.Core.Generators
{
    /// <summary>
    /// מעבד תבניות HTML - אחראי על עיבוד תבניות והחלפת פלייסהולדרים בערכים אמיתיים
    /// גרסה משופרת עם תמיכה בקישינג.
    /// </summary>
    public class HtmlTemplateProcessor
    {
        private readonly Dictionary<string, string> _columnMappings;
        private readonly IHandlebars _handlebars;
        
        // מילון פשוט לשמירת תבניות מקומפלות
        private readonly Dictionary<int, HandlebarsTemplate<object, object>> _templateCache;

        /// <summary>
        /// יוצר מופע חדש של מעבד תבניות
        /// </summary>
        /// <param name="errorManager">מנהל שגיאות</param>
        public HtmlTemplateProcessor()
        {
            _columnMappings = new Dictionary<string, string>();

            // מילון לקישינג תבניות
            _templateCache = new Dictionary<int, HandlebarsTemplate<object, object>>();

            // אתחול מנוע Handlebars
            _handlebars = Handlebars.Create(new HandlebarsConfiguration
            {
                NoEscape = true // מניעת אסקייפינג של HTML
            });

            RegisterHandlebarsHelpers();
        }

        /// <summary>
        /// פונקציה לעדכון מיפויי העמודות
        /// </summary>
        public void SetColumnMappings(Dictionary<string, string> columnMappings)
        {
            if (columnMappings != null)
            {
                _columnMappings.Clear();
                foreach (var mapping in columnMappings)
                {
                    _columnMappings[mapping.Key] = mapping.Value;
                }
            }
        }

        /// <summary>
        /// קבלת מיפויי העמודות הנוכחיים
        /// </summary>
        public Dictionary<string, string> GetColumnMappings()
        {
            return new Dictionary<string, string>(_columnMappings);
        }

        /// <summary>
        /// רישום הלפרים ל-Handlebars
        /// </summary>
        private void RegisterHandlebarsHelpers()
        {
            // ========== HELPERS לפורמט והצגה ==========

            // רישום הלפר לפורמט מספרים (קיים)
            _handlebars.RegisterHelper("format", (writer, context, parameters) => {
                if (parameters.Length > 0 && parameters[0] != null)
                {
                    writer.Write(FormatValue(parameters[0]));
                }
            });

            // הלפר לפורמט מספרים עם אפשרות לפורמט מותאם אישית
            _handlebars.RegisterHelper("formatNumber", (writer, context, parameters) =>
            {
                if (parameters.Length >= 1 && decimal.TryParse(parameters[0]?.ToString(), out decimal number))
                {
                    string format = parameters.Length > 1 ? parameters[1].ToString() : "N2";
                    writer.WriteSafeString(number.ToString(format));
                }
                else
                {
                    writer.WriteSafeString(parameters[0]?.ToString() ?? "");
                }
            });

            // הלפר לפורמט תאריכים
            _handlebars.RegisterHelper("formatDate", (writer, context, parameters) =>
            {
                if (parameters.Length >= 1 && DateTime.TryParse(parameters[0]?.ToString(), out DateTime date))
                {
                    string format = parameters.Length > 1 ? parameters[1].ToString() : "dd/MM/yyyy";
                    writer.WriteSafeString(date.ToString(format));
                }
                else
                {
                    writer.WriteSafeString(parameters[0]?.ToString() ?? "");
                }
            });

            // הלפר לקבלת שם עברי של עמודה (קיים)
            _handlebars.RegisterHelper("header", (writer, context, parameters) => {
                if (parameters.Length > 0 && parameters[0] != null)
                {
                    string columnName = parameters[0].ToString();
                    writer.Write(GetHebrewName(columnName, null));
                }
            });

            // ========== HELPERS להשוואות ==========

            // הלפר להשוואת ערכים - גרסה משופרת (משלב את הקיים)
            _handlebars.RegisterHelper("eq", (writer, options, context, arguments) => {
                if (arguments.Length >= 2)
                {
                    // השוואה עבור ערכים מספריים
                    if (decimal.TryParse(arguments[0]?.ToString(), out decimal val1) &&
                        decimal.TryParse(arguments[1]?.ToString(), out decimal val2))
                    {
                        if (val1 == val2)
                        {
                            options.Template(writer, context);
                        }
                        else
                        {
                            options.Inverse(writer, context);
                        }
                        return;
                    }

                    // השוואת בוליאנים
                    if (bool.TryParse(arguments[0]?.ToString(), out bool bool1) &&
                        bool.TryParse(arguments[1]?.ToString(), out bool bool2))
                    {
                        if (bool1 == bool2)
                        {
                            options.Template(writer, context);
                        }
                        else
                        {
                            options.Inverse(writer, context);
                        }
                        return;
                    }

                    // השוואת מחרוזות
                    string str1 = arguments[0]?.ToString() ?? string.Empty;
                    string str2 = arguments[1]?.ToString() ?? string.Empty;

                    if (str1.Equals(str2, StringComparison.OrdinalIgnoreCase))
                    {
                        options.Template(writer, context);
                    }
                    else
                    {
                        options.Inverse(writer, context);
                    }
                }
            });

            // Helper לאי-שוויון
            _handlebars.RegisterHelper("neq", (writer, options, context, arguments) =>
            {
                if (arguments.Length >= 2)
                {
                    // השוואה עבור ערכים מספריים
                    if (decimal.TryParse(arguments[0]?.ToString(), out decimal val1) &&
                        decimal.TryParse(arguments[1]?.ToString(), out decimal val2))
                    {
                        if (val1 != val2)
                        {
                            options.Template(writer, context);
                        }
                        else
                        {
                            options.Inverse(writer, context);
                        }
                        return;
                    }

                    // השוואת מחרוזות
                    string str1 = arguments[0]?.ToString() ?? string.Empty;
                    string str2 = arguments[1]?.ToString() ?? string.Empty;

                    if (!str1.Equals(str2, StringComparison.OrdinalIgnoreCase))
                    {
                        options.Template(writer, context);
                    }
                    else
                    {
                        options.Inverse(writer, context);
                    }
                }
            });

            // Helper לגדול מ
            _handlebars.RegisterHelper("gt", (writer, options, context, arguments) =>
            {
                if (arguments.Length >= 2 &&
                    decimal.TryParse(arguments[0]?.ToString(), out decimal num1) &&
                    decimal.TryParse(arguments[1]?.ToString(), out decimal num2))
                {
                    if (num1 > num2)
                    {
                        options.Template(writer, context);
                    }
                    else
                    {
                        options.Inverse(writer, context);
                    }
                }
            });

            // Helper לקטן מ
            _handlebars.RegisterHelper("lt", (writer, options, context, arguments) =>
            {
                if (arguments.Length >= 2 &&
                    decimal.TryParse(arguments[0]?.ToString(), out decimal num1) &&
                    decimal.TryParse(arguments[1]?.ToString(), out decimal num2))
                {
                    if (num1 < num2)
                    {
                        options.Template(writer, context);
                    }
                    else
                    {
                        options.Inverse(writer, context);
                    }
                }
            });

            // Helper לגדול או שווה
            _handlebars.RegisterHelper("gte", (writer, options, context, arguments) =>
            {
                if (arguments.Length >= 2 &&
                    decimal.TryParse(arguments[0]?.ToString(), out decimal num1) &&
                    decimal.TryParse(arguments[1]?.ToString(), out decimal num2))
                {
                    if (num1 >= num2)
                    {
                        options.Template(writer, context);
                    }
                    else
                    {
                        options.Inverse(writer, context);
                    }
                }
            });

            // Helper לקטן או שווה
            _handlebars.RegisterHelper("lte", (writer, options, context, arguments) =>
            {
                if (arguments.Length >= 2 &&
                    decimal.TryParse(arguments[0]?.ToString(), out decimal num1) &&
                    decimal.TryParse(arguments[1]?.ToString(), out decimal num2))
                {
                    if (num1 <= num2)
                    {
                        options.Template(writer, context);
                    }
                    else
                    {
                        options.Inverse(writer, context);
                    }
                }
            });

            // ========== HELPERS לבדיקות מיוחדות ==========

            // הלפר לבדיקת האם ערך שונה מאפס (קיים)
            _handlebars.RegisterHelper("notEqualZero", (writer, options, context, arguments) => {
                if (arguments.Length > 0 && arguments[0] != null && decimal.TryParse(arguments[0].ToString(), out decimal value))
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

            // הלפר לבדיקת האם ערך הוא שורת סיכום (קיים)
            _handlebars.RegisterHelper("isSummary", (writer, options, context, arguments) => {
                if (arguments.Length > 0 && arguments[0] != null)
                {
                    var value = arguments[0];
                    bool isSummaryRow = false;

                    // בדיקה אם זו שורת סיכום (לפי מספר או שדה isSummary)
                    if (int.TryParse(value.ToString(), out int intValue) && (intValue == -1 || intValue == 1))
                    {
                        isSummaryRow = true;
                    }

                    if (isSummaryRow)
                    {
                        options.Template(writer, context);
                    }
                    else
                    {
                        options.Inverse(writer, context);
                    }
                }
                else
                {
                    options.Inverse(writer, context);
                }
            });

            // הלפר ספציפי לתמיכה באחורה בeqIsSummary (קיים)
            _handlebars.RegisterHelper("eqIsSummary", (writer, options, context, arguments) => {
                if (arguments.Length > 0 && arguments[0] != null)
                {
                    if (int.TryParse(arguments[0].ToString(), out int value) && value == 1)
                    {
                        options.Template(writer, context);
                    }
                    else
                    {
                        options.Inverse(writer, context);
                    }
                }
                else
                {
                    options.Inverse(writer, context);
                }
            });

            // Helper לבדיקת ערך ריק
            _handlebars.RegisterHelper("isEmpty", (writer, options, context, arguments) =>
            {
                if (arguments.Length >= 1)
                {
                    var value = arguments[0];
                    bool isEmpty = value == null ||
                                  string.IsNullOrWhiteSpace(value.ToString()) ||
                                  (value.ToString() == "0");

                    if (isEmpty)
                    {
                        options.Template(writer, context);
                    }
                    else
                    {
                        options.Inverse(writer, context);
                    }
                }
            });

            // ========== HELPERS ללוגיקה ==========

            // Helper ללוגיקת AND
            _handlebars.RegisterHelper("and", (writer, options, context, arguments) =>
            {
                if (arguments.Length >= 2)
                {
                    bool result = true;
                    foreach (var arg in arguments)
                    {
                        if (string.IsNullOrEmpty(arg?.ToString()) ||
                            arg.ToString() == "false" ||
                            arg.ToString() == "0")
                        {
                            result = false;
                            break;
                        }
                    }

                    if (result)
                    {
                        options.Template(writer, context);
                    }
                    else
                    {
                        options.Inverse(writer, context);
                    }
                }
            });

            // Helper ללוגיקת OR
            _handlebars.RegisterHelper("or", (writer, options, context, arguments) =>
            {
                if (arguments.Length >= 2)
                {
                    bool result = false;
                    foreach (var arg in arguments)
                    {
                        if (!string.IsNullOrEmpty(arg?.ToString()) &&
                            arg.ToString() != "false" &&
                            arg.ToString() != "0")
                        {
                            result = true;
                            break;
                        }
                    }

                    if (result)
                    {
                        options.Template(writer, context);
                    }
                    else
                    {
                        options.Inverse(writer, context);
                    }
                }
            });

            // Helper לשלילה (NOT)
            _handlebars.RegisterHelper("not", (writer, options, context, arguments) =>
            {
                if (arguments.Length >= 1)
                {
                    var value = arguments[0];
                    bool isFalsy = value == null ||
                                  string.IsNullOrEmpty(value.ToString()) ||
                                  value.ToString() == "false" ||
                                  value.ToString() == "0";

                    if (isFalsy)
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
                    SimpleLogger.LogError("תבנית HTML לא יכולה להיות ריקה");
                    throw new ArgumentException("Template cannot be null or empty");
                }

                // דיבוג - רישום מספר השורות בכל טבלה
                if (dataTables != null)
                {
                    foreach (var tableEntry in dataTables)
                    {
                        SimpleLogger.LogInfo($"טבלה {tableEntry.Key}: {tableEntry.Value.Rows.Count} שורות, {tableEntry.Value.Columns.Count} עמודות");
                        
                        // רישום שמות העמודות
                        List<string> columnNames = new List<string>();
                        foreach (DataColumn col in tableEntry.Value.Columns)
                        {
                            columnNames.Add(col.ColumnName);
                        }
                        
                        SimpleLogger.LogInfo($"עמודות בטבלה {tableEntry.Key}: {string.Join(", ", columnNames)}");
                    }
                }

                // 1. החלפת HEADER: פלייסהולדרים
                string processedTemplate = PreProcessHeaders(template);
                
                // 2. הכנת נתונים למודל
                var templateModel = PrepareTemplateModel(values, dataTables);
                
                // רישום דיבוג של המודל
                SimpleLogger.LogInfo($"מספר מפתחות במודל: {(templateModel as Dictionary<string, object>)?.Count ?? 0}");
                
                List<string> modelKeys = new List<string>();
                if (templateModel is Dictionary<string, object> modelDict)
                {
                    foreach (var key in modelDict.Keys)
                    {
                        modelKeys.Add(key);
                    }
                }
                
                SimpleLogger.LogInfo( $"מפתחות במודל: {string.Join(", ", modelKeys)}");
                
                // שימוש ב-cache key המבוסס על תבנית
                int cacheKey = processedTemplate.GetHashCode();
                
                // ניסיון לקבל תבנית מקומפלת מה-cache
                HandlebarsTemplate<object, object> compiledTemplate;
                
                lock (_templateCache)
                {
                    if (!_templateCache.TryGetValue(cacheKey, out compiledTemplate))
                    {
                        try
                        {
                            // אם אין ב-cache, קומפילציה חדשה
                            compiledTemplate = _handlebars.Compile(processedTemplate);
                            
                            // שמירה ב-cache
                            _templateCache[cacheKey] = compiledTemplate;
                        }
                        catch (Exception ex)
                        {
                            // ניסיון תיקון אוטומטי של בעיות תחביר נפוצות
                            string fixedTemplate = Regex.Replace(
                                processedTemplate, 
                                @"\{\{#if\s+([^=\s]+)\s+=\s+([^}]+)\}\}", 
                                "{{#if $1 == $2}}"  // תיקון סימן שוויון בודד לכפול
                            );
                            
                            // ניסיון בCompilation החדש
                            SimpleLogger.LogWarning($"תיקון אוטומטי של תחביר תנאי:  {ex.Message}");
                                
                            compiledTemplate = _handlebars.Compile(fixedTemplate);
                            
                            // שמירה ב-cache
                            _templateCache[cacheKey] = compiledTemplate;
                        }
                    }
                }
                
                try {
                    // 3. עיבוד התבנית באמצעות המנוע
                    string result = compiledTemplate(templateModel);

                    // דיבוג - רישום האם התוצאה מכילה פלייסהולדרים
                    bool containsPlaceholders = result.Contains("{{") && result.Contains("}}");
                    SimpleLogger.LogInfo(containsPlaceholders ? "התוצאה עדיין מכילה פלייסהולדרים שלא הוחלפו!"
                            : "כל הפלייסהולדרים הוחלפו בהצלחה.");
                    
                    // 4. טיפול בפלייסהולדרים של מספור עמודים
                    result = HandlePagePlaceholders(result);

                    return result;
                }
                catch (Exception ex) {
                    SimpleLogger.LogError($"שגיאה בעיבוד התבנית: {ex.Message}", ex);
                    
                    // במקרה של שגיאה בעיבוד התבנית, ננסה לעבד את התבנית ללא תבניות דינמיות
                    // ורק עם פלייסהולדרים פשוטים
                    return ProcessFallbackTemplate(template, values, dataTables);
                }
            }
            catch (Exception ex) when (!(ex is ArgumentException))
            {
                SimpleLogger.LogError( "שגיאה בעיבוד תבנית HTML", ex);
                throw new Exception("Error processing HTML template", ex);
            }
        }
        
        /// <summary>
        /// מעבד את התבנית באופן פשוט יותר (ללא Handlebars) במקרה של שגיאה
        /// </summary>
        private string ProcessFallbackTemplate(
            string template,
            Dictionary<string, object> values,
            Dictionary<string, DataTable> dataTables)
        {
            try
            {
                SimpleLogger.LogWarning("שימוש בעיבוד גיבוי פשוט (ללא Handlebars)");

                // 1. החלפת פלייסהולדרים פשוטים
                string result = ProcessSimplePlaceholders(template, values);
                
                // 2. עיבוד טבלאות נתונים
                result = ProcessDynamicTablesManually(result, dataTables);
                
                // 3. טיפול בהדרים
                result = ProcessHeadersManually(result);
                
                // 4. טיפול בפלייסהולדרים של מספור עמודים
                result = HandlePagePlaceholders(result);
                
                return result;
            }
            catch (Exception ex)
            {
                SimpleLogger.LogError( "גם עיבוד הגיבוי נכשל", ex);
                return template; // החזרת התבנית המקורית
            }
        }
        
        /// <summary>
        /// מעבד פלייסהולדרים פשוטים באופן ידני (ללא Handlebars)
        /// </summary>
        private string ProcessSimplePlaceholders(string template, Dictionary<string, object> values)
        {
            string result = template;
            
            // הוספת ערכים מובנים
            result = result.Replace("{{CurrentDate}}", DateTime.Now.ToString("dd/MM/yyyy"));
            result = result.Replace("{{CurrentTime}}", DateTime.Now.ToString("HH:mm:ss"));
            
            // הוספת פרמטרים שהועברו
            if (values != null)
            {
                foreach (var entry in values)
                {
                    string placeholder = $"{{{{{entry.Key}}}}}";
                    string value = FormatValue(entry.Value);
                    result = result.Replace(placeholder, value);
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// מעבד כותרות באופן ידני (ללא Handlebars)
        /// </summary>
        private string ProcessHeadersManually(string template)
        {
            // ביטוי רגולרי לאיתור {{HEADER:XXX}}
            var matches = Regex.Matches(template, @"\{\{HEADER:([^}]+)\}\}");
            string result = template;
            
            foreach (Match match in matches)
            {
                string columnName = match.Groups[1].Value;
                string hebrewHeader = GetHebrewName(columnName, null);
                result = result.Replace(match.Value, hebrewHeader);
            }
            
            return result;
        }
        
        /// <summary>
        /// מעבד טבלאות דינמיות באופן ידני (ללא Handlebars)
        /// </summary>
        private string ProcessDynamicTablesManually(string template, Dictionary<string, DataTable> dataTables)
        {
        if (dataTables == null || dataTables.Count == 0)
        return template;

        string result = template;
        
        // ביטוי רגולרי לאיתור תגים עם data-table-row
        var matches = Regex.Matches(result, 
        @"<(tr|div)[^>]*data-table-row=""([^""]+)""[^>]*>(.*?)</\1>",
        RegexOptions.Singleline);

        foreach (Match match in matches)
        {
        string tagName = match.Groups[1].Value;
        string tableName = match.Groups[2].Value;
        string rowTemplate = match.Groups[3].Value;
        
        // בדיקה אם יש טבלה כזו בנתונים
        DataTable table = null;
        
        // חיפוש ישיר
        if (dataTables.ContainsKey(tableName))
        {
        table = dataTables[tableName];
        }
        // חיפוש עם/בלי dbo.
        else
        {
        string altTableName = tableName.StartsWith("dbo.") 
        ? tableName.Substring(4) 
        : $"dbo.{tableName}";
        
        if (dataTables.ContainsKey(altTableName))
        {
        table = dataTables[altTableName];
        }
        }
        
        if (table == null || table.Rows.Count == 0)
        {
        // אין נתונים
        SimpleLogger.LogWarning($"לא נמצאו נתונים לטבלה: {tableName}");
        continue;
        }
        
        // עיבוד השורות
        StringBuilder rowsBuilder = new StringBuilder();
        
        foreach (DataRow row in table.Rows)
        {
        string currentRow = rowTemplate;
        
        // החלפת כל השדות בערכים - גם באופנים נוספים
        foreach (DataColumn col in table.Columns)
        {
        // בדיקת פלייסהולדרים בשלושה פורמטים
        string plainPlaceholder = $"{{{{{col.ColumnName}}}}}"; 
        string tablePlaceholder = $"{{{{{tableName}_{col.ColumnName}}}}}"; 
            string qualifiedPlaceholder = $"{{{{{tableName}.{col.ColumnName}}}}}"; 
            
            string value = FormatValue(row[col] == DBNull.Value ? null : row[col]);
            
        // החלפת כל הפורמטים של פלייסהולדרים
            currentRow = currentRow.Replace(plainPlaceholder, value);
        currentRow = currentRow.Replace(tablePlaceholder, value);
                currentRow = currentRow.Replace(qualifiedPlaceholder, value);
            }
            
            // הוספת השורה החדשה
                if (tagName.ToLower() == "tr")
                    rowsBuilder.AppendLine($"<tr>{currentRow}</tr>");
                else
                        rowsBuilder.Append($"<{tagName}>{currentRow}</{tagName}>");
                }
                
                // החלפת השורה המקורית עם כל השורות החדשות
                result = result.Replace(match.Value, rowsBuilder.ToString());
            }
            
            return result;
        }
        
        /// <summary>
        /// מחליף פלייסהולדרים של כותרות ב-syntax של Handlebars
        /// </summary>
        private string PreProcessHeaders(string template)
        {
            try
            {
                // המרת {{HEADER:XXX}} ל-{{header 'XXX'}}
                return System.Text.RegularExpressions.Regex.Replace(
                    template, 
                    @"\{\{HEADER:([^}]+)\}\}", 
                    match => $"{{{{header '{match.Groups[1].Value}'}}}}");
            }
            catch (Exception ex)
            {
                SimpleLogger.LogWarning("שגיאה בעיבוד כותרות", template,ex.Message);
                return template;
            }
        }
        
        /// <summary>
        /// מכין את המודל הכולל לתבנית
        /// </summary>
        private object PrepareTemplateModel(Dictionary<string, object> values, Dictionary<string, DataTable> dataTables)
        {
            // מודל בסיסי עם ערכים פשוטים
            var templateModel = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            
            // הוספת ערכים מובנים
            templateModel["CurrentDate"] = DateTime.Now.ToString("dd/MM/yyyy");
            templateModel["CurrentTime"] = DateTime.Now.ToString("HH:mm:ss");
            
            // הוספת פרמטרים שהועברו
            if (values != null)
            {
                foreach (var entry in values)
                {
                    templateModel[entry.Key] = FormatValue(entry.Value);
                }
            }
            
            // הוספת טבלאות נתונים
            if (dataTables != null)
            {
                foreach (var tableEntry in dataTables)
                {
                    string tableKey = tableEntry.Key;
                    var table = ConvertDataTableToList(tableEntry.Value);
                    
                    // אם אין נתונים, הוסף רשימה ריקה כדי שההלפרים יעבדו
                    if (table.Count == 0)
                    {
                        SimpleLogger.LogWarning( $"אין נתונים בטבלה {tableKey} - יצירת רשומה ריקה כדי שההלפרים יעבדו.");
                            
                        var emptyItem = new Dictionary<string, object>();
                        foreach (DataColumn col in tableEntry.Value.Columns)
                        {
                            emptyItem[col.ColumnName] = null;
                        }
                        table.Add(emptyItem);
                    }
                    
                    // רישום דיבוג של הנתונים המומרים
                    SimpleLogger.LogInfo( $"טבלה {tableKey} לאחר המרה: {table.Count} רשומות");
                        
                    if (table.Count > 0)
                    {
                        var firstItem2 = table[0];
                        List<string> keys = new List<string>();
                        foreach (var key in firstItem2.Keys)
                        {
                            keys.Add(key);
                        }
                        
                        SimpleLogger.LogInfo( $"שדות ברשומה ראשונה של {tableKey}: {string.Join(", ", keys)}");
                    }
                    
                    // אחסון במודל - שינוי: עכשיו נשמור גם ברמת השדות בודדים וגם כרשימה
                    var firstItem = table.Count > 0 ? table[0] : new Dictionary<string, object>();
                    templateModel[tableKey] = firstItem;
                    
                    // הוספת השדות גם ברמה העליונה ישירות (לתמיכה בפלייסהולדרים פשוטים)
                    if (table.Count > 0) {
                        foreach(var key in firstItem.Keys) {
                            string fullKey = $"{tableKey}_{key}";
                            if (!templateModel.ContainsKey(fullKey)) {
                                templateModel[fullKey] = firstItem[key];
                            }
                        }
                    }
                    
                    // אחסון גם את הרשימה המלאה לשימוש בלולאות
                    templateModel[tableKey + "_list"] = table;
                    
                    // הוספת גישה גם ללא קידומת של dbo.
                    if (tableKey.StartsWith("dbo."))
                    {
                        string shortKey = tableKey.Substring(4);
                        if (!templateModel.ContainsKey(shortKey))
                        {
                            templateModel[shortKey] = table.Count > 0 ? table[0] : new Dictionary<string, object>();
                            templateModel[shortKey + "_list"] = table;
                        }
                    }
                }
            }
            
            return templateModel;
        }
        
        /// <summary>
        /// ממיר DataTable לרשימת מילונים לשימוש ב-Handlebars
        /// </summary>
        private List<Dictionary<string, object>> ConvertDataTableToList(DataTable table)
        {
            var list = new List<Dictionary<string, object>>();
            
            foreach (DataRow row in table.Rows)
            {
                var item = new Dictionary<string, object>();
                
                foreach (DataColumn col in table.Columns)
                {
                    object value = row[col] == DBNull.Value ? null : row[col];
                    item[col.ColumnName] = value;
                }
                
                list.Add(item);
            }
            
            return list;
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
                SimpleLogger.LogWarning($"שגיאה בקבלת שם עברי לעמודה {columnName}",ex.Message, columnName);

                return columnName; // במקרה של שגיאה, החזרת השם המקורי
            }
        }

        /// <summary>
        /// טיפול בפלייסהולדרים של מספור עמודים
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
                SimpleLogger.LogWarning("שגיאה בעיבוד פלייסהולדרים של מספרי עמודים", template, ex.Message);
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
    }
}