using OfficeOpenXml;
using OfficeOpenXml.Style;
using ReportGenerator.Core.Errors;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using System.Text.RegularExpressions;

namespace ReportGenerator.Core.Generators
{
    /// <summary>
    /// מחולל קבצי Excel - מייצר קבצי Excel מנתוני הדוח
    /// </summary>
    public class ExcelGenerator
    {
        private readonly Dictionary<string, string> _columnMappings;

        /// <summary>
        /// יוצר מופע חדש של מחולל האקסל
        /// </summary>
        /// <param name="columnMappings">מילון מיפויים של שמות עמודות לשמות בעברית</param>
        public ExcelGenerator(Dictionary<string, string> columnMappings = null)
        {
            _columnMappings = columnMappings ?? new Dictionary<string, string>();
            
            // הגדרת רישיון EPPlus כלא מסחרי לצורכי פיתוח
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        }

        /// <summary>
        /// יצירת קובץ Excel מנתוני הדוחות
        /// </summary>
        /// <param name="dataTables">מילון של טבלאות נתונים (מפתח = שם הטבלה)</param>
        /// <param name="reportTitle">כותרת הדוח</param>
        /// <returns>מערך בייטים של קובץ האקסל</returns>
        public byte[] Generate(Dictionary<string, DataTable> dataTables, string reportTitle)
        {
            if (dataTables == null || dataTables.Count == 0)
            {
                ErrorManager.LogError(
                    ErrorCodes.Excel.Data_Format_Invalid,
                    ErrorSeverity.Critical,
                    "לא ניתן לייצר קובץ Excel ללא נתונים");
                throw new ArgumentException("No data provided for Excel generation");
            }

            try
            {
                using var package = new ExcelPackage();
                int sheetIndex = 0;
                
                foreach (var tableEntry in dataTables)
                {
                    string tableName = tableEntry.Key;
                    DataTable data = tableEntry.Value;
                    
                    // יצירת שם לגיליון
                    string sheetName = GetValidSheetName(tableName, sheetIndex);
                    sheetIndex++;
                    
                    // יצירת גיליון חדש
                    var worksheet = package.Workbook.Worksheets.Add(sheetName);
                    
                    // הגדרת כיווניות RTL
                    worksheet.View.RightToLeft = true;
                    
                    // הוספת כותרת הדוח
                    AddReportTitle(worksheet, reportTitle, data.Columns.Count);
                    
                    // הוספת כותרות בעברית
                    AddHebrewHeaders(worksheet, data, tableName);
                    
                    // הוספת הנתונים
                    AddDataRows(worksheet, data);
                    
                    // עיצוב הגיליון
                    FormatWorksheet(worksheet, data);
                }
                
                byte[] excelBytes = package.GetAsByteArray();
                
                ErrorManager.LogInfo(
                    "Excel_Generation_Success",
                    $"קובץ Excel נוצר בהצלחה. גודל: {excelBytes.Length / 1024:N0} KB");
                    
                return excelBytes;
            }
            catch (Exception ex)
            {
                ErrorManager.LogError(
                    ErrorCodes.Excel.Generation_Failed,
                    ErrorSeverity.Critical,
                    "שגיאה ביצירת קובץ Excel",
                    ex);
                throw new Exception("Failed to generate Excel file", ex);
            }
        }

        /// <summary>
        /// קבלת שם תקף לגיליון אקסל
        /// </summary>
        private string GetValidSheetName(string originalName, int index)
        {
            // הסרת תווים לא חוקיים
            string safeName = Regex.Replace(originalName, @"[\[\]\*\?/\\]", "_");
            
            // ניקוי מנקודות, dbo וכו'
            safeName = safeName.Replace("dbo.", "")
                .Replace(".", "_")
                .Trim();
                
            // הגבלת אורך שם הגיליון ל-31 תווים (מגבלה של Excel)
            if (safeName.Length > 31)
            {
                safeName = safeName.Substring(0, 28) + "_" + index;
            }
            
            // אם השם ריק, שימוש בשם ברירת מחדל
            if (string.IsNullOrWhiteSpace(safeName))
            {
                safeName = $"Sheet_{index + 1}";
            }
            
            return safeName;
        }

        /// <summary>
        /// הוספת כותרת הדוח לגיליון
        /// </summary>
        private void AddReportTitle(ExcelWorksheet worksheet, string reportTitle, int columnCount)
        {
            try
            {
                // הוספת כותרת הדוח
                worksheet.Cells[1, 1].Value = reportTitle;
                if (columnCount > 1)
                {
                    worksheet.Cells[1, 1, 1, columnCount].Merge = true;
                }
                
                // עיצוב כותרת
                var titleCell = worksheet.Cells[1, 1, 1, columnCount];
                titleCell.Style.Font.Bold = true;
                titleCell.Style.Font.Size = 14;
                titleCell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                titleCell.Style.Fill.PatternType = ExcelFillStyle.Solid;
                titleCell.Style.Fill.BackgroundColor.SetColor(Color.LightSteelBlue);
                
                // הוספת תאריך הפקה
                worksheet.Cells[2, 1].Value = $"תאריך הפקה: {DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss")}";
                if (columnCount > 1)
                {
                    worksheet.Cells[2, 1, 2, columnCount].Merge = true;
                }
                
                // עיצוב תאריך
                worksheet.Cells[2, 1, 2, columnCount].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                worksheet.Cells[2, 1, 2, columnCount].Style.Font.Bold = true;
            }
            catch (Exception ex)
            {
                ErrorManager.LogWarning(
                    ErrorCodes.Excel.Generation_Failed,
                    $"שגיאה בהוספת כותרת הדוח לקובץ Excel: {ex.Message}");
                // ממשיך בכל זאת כדי ליצור את הקובץ
            }
        }

        /// <summary>
        /// הוספת כותרות בעברית לגיליון
        /// </summary>
        private void AddHebrewHeaders(ExcelWorksheet worksheet, DataTable data, string tableName)
        {
            try
            {
                // שורת כותרות (אחרי הכותרת הראשית ותאריך ההפקה)
                const int headerRow = 4;
                
                // הוספת כותרות
                for (int col = 0; col < data.Columns.Count; col++)
                {
                    string columnName = data.Columns[col].ColumnName;
                    string hebrewHeader = GetHebrewHeaderName(columnName, tableName);
                    
                    // הוספת כותרת בעברית
                    worksheet.Cells[headerRow, col + 1].Value = hebrewHeader;
                    
                    // עיצוב תא הכותרת
                    var headerCell = worksheet.Cells[headerRow, col + 1];
                    headerCell.Style.Font.Bold = true;
                    headerCell.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    headerCell.Style.Fill.BackgroundColor.SetColor(Color.LightGray);
                    headerCell.Style.Border.BorderAround(ExcelBorderStyle.Thin);
                    headerCell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                }
            }
            catch (Exception ex)
            {
                ErrorManager.LogWarning(
                    ErrorCodes.Excel.Column_Mapping_Failed,
                    $"שגיאה בהוספת כותרות בעברית לקובץ Excel: {ex.Message}");
                // ממשיך בכל זאת כדי ליצור את הקובץ
            }
        }

        /// <summary>
        /// קבלת כותרת בעברית לעמודה
        /// </summary>
        private string GetHebrewHeaderName(string columnName, string tableName)
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
                    string compositeKey = columnName;
                    
                    // בדיקה אם יש מיפוי לשם המורכב
                    if (_columnMappings.TryGetValue(compositeKey, out mappedName))
                        return mappedName;
                }
                
                // אם לא נמצא מיפוי, מחזיר את השם המקורי
                return columnName;
            }
            catch (Exception ex)
            {
                ErrorManager.LogWarning(
                    ErrorCodes.Excel.Column_Mapping_Failed,
                    $"שגיאה בקבלת שם עברי לעמודה {columnName}: {ex.Message}");
                return columnName; // במקרה של שגיאה, מחזיר את השם המקורי
            }
        }

        /// <summary>
        /// הוספת שורות נתונים לגיליון
        /// </summary>
        private void AddDataRows(ExcelWorksheet worksheet, DataTable data)
        {
            try
            {
                // התחלה משורה 5 (אחרי כותרת הדוח, תאריך הפקה ושורת כותרות)
                const int startRow = 5;
                
                // הוספת כל שורות הנתונים
                for (int row = 0; row < data.Rows.Count; row++)
                {
                    for (int col = 0; col < data.Columns.Count; col++)
                    {
                        // הוספת הערך
                        var cell = worksheet.Cells[startRow + row, col + 1];
                        var value = data.Rows[row][col];
                        
                        if (value != DBNull.Value && value != null)
                        {
                            cell.Value = value;
                            
                            // עיצוב בהתאם לסוג הנתונים
                            ApplyCellFormatting(cell, value);
                        }
                        else
                        {
                            cell.Value = string.Empty;
                        }
                        
                        // עיצוב בסיסי לכל תא
                        cell.Style.Border.BorderAround(ExcelBorderStyle.Thin);
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorManager.LogWarning(
                    ErrorCodes.Excel.Data_Format_Invalid,
                    $"שגיאה בהוספת נתונים לקובץ Excel: {ex.Message}");
                // ממשיך בכל זאת כדי ליצור את הקובץ
            }
        }

        /// <summary>
        /// הוספת עיצוב בהתאם לסוג הנתונים
        /// </summary>
        private void ApplyCellFormatting(ExcelRange cell, object value)
        {
            if (value is decimal || value is double || value is float)
            {
                cell.Style.Numberformat.Format = "#,##0.00";
                cell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
            }
            else if (value is int || value is long)
            {
                cell.Style.Numberformat.Format = "#,##0";
                cell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
            }
            else if (value is DateTime)
            {
                cell.Style.Numberformat.Format = "dd/mm/yyyy";
                cell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            }
            else if (value is bool)
            {
                cell.Value = (bool)value ? "כן" : "לא";
                cell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            }
            else
            {
                cell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
            }
        }

        /// <summary>
        /// עיצוב כללי של הגיליון
        /// </summary>
        private void FormatWorksheet(ExcelWorksheet worksheet, DataTable data)
        {
            try
            {
                // התאמת רוחב עמודות באופן אוטומטי
                worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();
                
                // מינימום רוחב לעמודות צרות מדי
                const double minColumnWidth = 8;
                
                for (int col = 1; col <= data.Columns.Count; col++)
                {
                    double currentWidth = worksheet.Column(col).Width;
                    if (currentWidth < minColumnWidth)
                    {
                        worksheet.Column(col).Width = minColumnWidth;
                    }
                    else if (currentWidth > 100) // מקסימום רוחב
                    {
                        worksheet.Column(col).Width = 100;
                    }
                }
                
                // הגדרת קפיאת שורות כותרת
                worksheet.View.FreezePanes(5, 1);
            }
            catch (Exception ex)
            {
                ErrorManager.LogWarning(
                    ErrorCodes.Excel.Style_Invalid,
                    $"שגיאה בעיצוב גיליון Excel: {ex.Message}");
                // ממשיך בכל זאת כדי ליצור את הקובץ
            }
        }
    }
}
