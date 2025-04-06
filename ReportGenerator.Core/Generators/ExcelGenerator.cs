using OfficeOpenXml;
using ReportGenerator.Core.Services;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Threading.Tasks;

namespace ReportGenerator.Core.Generators
{
    /// <summary>
    /// יוצר קבצי אקסל מנתונים
    /// </summary>
    public class ExcelGenerator : IExcelGenerator
    {
        private readonly Dictionary<string, string> _columnMappings;
        private readonly ConcurrentDictionary<string, string> _headerMappingCache = 
            new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// יוצר מופע חדש של יוצר האקסל
        /// </summary>
        /// <param name="columnMappings">מילון המיפויים בין שמות עמודות באנגלית לעברית</param>
        public ExcelGenerator(Dictionary<string, string> columnMappings = null)
        {
            _columnMappings = columnMappings ?? new Dictionary<string, string>();
            
            // הגדרת רישיון EPPlus
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        }

        /// <summary>
        /// ניקוי המטמון
        /// </summary>
        public void ClearCache()
        {
            _headerMappingCache.Clear();
        }

        /// <summary>
        /// מייצר קובץ אקסל מטבלאות נתונים
        /// </summary>
        /// <param name="dataTables">מילון של טבלאות נתונים</param>
        /// <param name="reportTitle">כותרת הדוח</param>
        /// <returns>מערך בייטים של קובץ אקסל</returns>
        public async Task<byte[]> GenerateAsync(Dictionary<string, DataTable> dataTables, string reportTitle)
        {
            if (dataTables == null || dataTables.Count == 0)
                throw new ArgumentException("No data provided for Excel generation");

            return await Task.Run(() => {
                using (var package = new ExcelPackage())
                {
                    int worksheetCounter = 1;
                    foreach (var tableEntry in dataTables)
                    {
                        string tableName = tableEntry.Key;
                        DataTable data = tableEntry.Value;

                        if (data == null || data.Columns.Count == 0)
                            continue;

                        // יצירת שם מתאים לגיליון
                        string worksheetName = GetSafeSheetName(tableName, worksheetCounter);
                        var worksheet = package.Workbook.Worksheets.Add(worksheetName);

                        // הגדרת כיווניות RTL
                        worksheet.View.RightToLeft = true;

                        // הוספת כותרת הדוח
                        worksheet.Cells[1, 1].Value = reportTitle;
                        worksheet.Cells[1, 1, 1, data.Columns.Count].Merge = true;
                        worksheet.Cells[1, 1].Style.Font.Bold = true;
                        worksheet.Cells[1, 1].Style.Font.Size = 14;
                        worksheet.Cells[1, 1].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;

                        // הוספת תאריך הפקה
                        worksheet.Cells[2, 1].Value = $"תאריך הפקה: {DateTime.Now:dd/MM/yyyy}";
                        worksheet.Cells[2, 1, 2, data.Columns.Count].Merge = true;
                        worksheet.Cells[2, 1].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;

                        // הוספת כותרות בעברית
                        for (int col = 0; col < data.Columns.Count; col++)
                        {
                            string columnName = data.Columns[col].ColumnName;
                            string hebrewHeader = GetHebrewName(columnName, tableName);

                            worksheet.Cells[4, col + 1].Value = hebrewHeader;
                            worksheet.Cells[4, col + 1].Style.Font.Bold = true;
                            worksheet.Cells[4, col + 1].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                            worksheet.Cells[4, col + 1].Style.Fill.BackgroundColor.SetColor(Color.LightGray);
                            worksheet.Cells[4, col + 1].Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin);
                        }

                        // הוספת נתונים
                        for (int row = 0; row < data.Rows.Count; row++)
                        {
                            for (int col = 0; col < data.Columns.Count; col++)
                            {
                                var cell = worksheet.Cells[row + 5, col + 1];
                                var value = data.Rows[row][col];
                                
                                // הגדרת סוג התא לפי סוג הנתונים
                                SetCellValue(cell, value);
                                
                                // עיצוב תא
                                cell.Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin);

                                // התאמת סוג תא לפי סוג הנתונים
                                if (value != null && value != DBNull.Value)
                                {
                                    if (value is decimal || value is double)
                                    {
                                        cell.Style.Numberformat.Format = "#,##0.00";
                                        cell.Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Right;
                                    }
                                    else if (value is DateTime)
                                    {
                                        cell.Style.Numberformat.Format = "dd/mm/yyyy";
                                    }
                                }
                            }
                        }

                        // התאמת רוחב עמודות
                        worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();
                        
                        worksheetCounter++;
                    }

                    return package.GetAsByteArray();
                }
            });
        }

        /// <summary>
        /// מקבל את השם העברי של עמודה לפי שם העמודה באנגלית וההקשר (טבלה/פרוצדורה)
        /// </summary>
        /// <param name="columnName">שם העמודה באנגלית</param>
        /// <param name="context">הקשר (טבלה/פרוצדורה)</param>
        /// <returns>השם העברי של העמודה</returns>
        private string GetHebrewName(string columnName, string context)
        {
            try
            {
                // בדיקה במטמון קודם
                string cacheKey = $"{context ?? ""}_{columnName}";
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
                    // שדה מחושב - חיפוש לפי שם השדה או לפי הקשר ושם שדה
                    if (_columnMappings.TryGetValue(columnName, out string mappedName))
                        result = mappedName;
                    else if (context != null && _columnMappings.TryGetValue($"{context}.{columnName}", out mappedName))
                        result = mappedName;
                    else
                        result = columnName;
                }
                
                // הוספה למטמון
                _headerMappingCache[cacheKey] = result;
                
                return result;
            }
            catch (Exception)
            {
                return columnName; // במקרה של שגיאה, החזרת השם המקורי
            }
        }

        /// <summary>
        /// הגדרת ערך של תא אקסל בהתאם לסוג הנתונים
        /// </summary>
        private void SetCellValue(OfficeOpenXml.ExcelRange cell, object value)
        {
            if (value == null || value == DBNull.Value)
            {
                cell.Value = null;
                return;
            }

            // טיפול לפי סוג הנתונים
            if (value is DateTime dateValue)
            {
                cell.Value = dateValue;
                cell.Style.Numberformat.Format = "dd/mm/yyyy";
            }
            else if (value is bool boolValue)
            {
                cell.Value = boolValue;
            }
            else if (value is decimal decimalValue)
            {
                cell.Value = decimalValue;
                cell.Style.Numberformat.Format = "#,##0.00";
            }
            else if (value is double doubleValue)
            {
                cell.Value = doubleValue;
                cell.Style.Numberformat.Format = "#,##0.00";
            }
            else if (value is float floatValue)
            {
                cell.Value = floatValue;
                cell.Style.Numberformat.Format = "#,##0.00";
            }
            else if (value is int || value is long || value is short)
            {
                cell.Value = value;
                cell.Style.Numberformat.Format = "#,##0";
            }
            else
            {
                cell.Value = value.ToString();
            }
        }

        /// <summary>
        /// מייצר שם בטוח לגיליון אקסל (ללא תווים לא חוקיים)
        /// </summary>
        private string GetSafeSheetName(string name, int counter)
        {
            // הסרת תווים לא חוקיים בשם גיליון
            string safeName = name.Replace("dbo.", "")
                                 .Replace("[", "")
                                 .Replace("]", "")
                                 .Replace("'", "")
                                 .Replace("\"", "")
                                 .Replace(":", "")
                                 .Replace("/", "")
                                 .Replace("\\", "")
                                 .Replace("?", "")
                                 .Replace("*", "");

            // קיצור השם אם ארוך מדי
            if (safeName.Length > 28)
            {
                safeName = safeName.Substring(0, 28);
            }

            // הוספת מספר סידורי אם צריך
            return $"{safeName}_{counter}";
        }

        /// <summary>
        /// מייצר קובץ אקסל - מימוש תאימות אחורה עם ממשק ישן
        /// </summary>
        public byte[] Generate(Dictionary<string, DataTable> dataTables, string reportTitle)
        {
            return GenerateAsync(dataTables, reportTitle).GetAwaiter().GetResult();
        }
    }
}