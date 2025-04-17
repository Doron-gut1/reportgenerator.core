using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using ReportGenerator.Core.Errors;
using ReportGenerator.Core.Interfaces;

namespace ReportGenerator.Core.Generators
{
    public class ExcelGenerator : IExcelGenerator
    {
        private readonly Dictionary<string, string> _columnMappings;
        private readonly List<string> _hiddenColumns = new List<string> { "IsSummary" };
        private readonly IErrorManager _errorManager;

        /// <summary>
        /// יוצר מופע חדש של יוצר קבצי אקסל
        /// </summary>
        /// <param name="columnMappings">מילון מיפויים בין שמות עמודות באנגלית לעברית</param>
        /// <param name="errorManager">מנהל שגיאות</param>
        public ExcelGenerator(Dictionary<string, string> columnMappings, IErrorManager errorManager)
        {
            _columnMappings = columnMappings ?? new Dictionary<string, string>();
            _errorManager = errorManager ?? throw new ArgumentNullException(nameof(errorManager));
        }

        /// <summary>
        /// יוצר קובץ אקסל ממספר טבלאות נתונים
        /// </summary>
        /// <param name="dataTables">מילון טבלאות נתונים</param>
        /// <param name="reportTitle">כותרת הדוח</param>
        /// <returns>מערך בייטים של קובץ אקסל</returns>
        public byte[] Generate(Dictionary<string, DataTable> dataTables, string reportTitle)
        {
            if (dataTables == null || dataTables.Count == 0)
            {
                _errorManager.LogError(
                    ErrorCodes.Excel.Generation_Failed,
                    ErrorSeverity.Error,
                    "No data provided for Excel generation");
                throw new ArgumentException("No data provided for Excel generation");
            }

            try
            {
                ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

                using (var package = new ExcelPackage())
                {
                    foreach (var tableEntry in dataTables)
                    {
                        string tableName = tableEntry.Key;
                        DataTable data = tableEntry.Value;

                        if (data.Rows.Count == 0)
                            continue;

                        // יצירת גיליון חדש
                        var worksheet = package.Workbook.Worksheets.Add(tableName);

                        // הגדרת כיווניות RTL
                        worksheet.View.RightToLeft = true;

                        // הוספת כותרת הדוח
                        worksheet.Cells[1, 1].Value = reportTitle;
                        worksheet.Cells[1, 1, 1, data.Columns.Count].Merge = true;
                        worksheet.Cells[1, 1].Style.Font.Bold = true;
                        worksheet.Cells[1, 1].Style.Font.Size = 14;
                        worksheet.Cells[1, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

                        // הוספת תאריך הפקה
                        worksheet.Cells[2, 1].Value = $"תאריך הפקה: {DateTime.Now:dd/MM/yyyy}";
                        worksheet.Cells[2, 1, 2, data.Columns.Count].Merge = true;
                        worksheet.Cells[2, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

                        // יצירת רשימת עמודות שיוצגו (ללא העמודות המוסתרות)
                        var visibleColumns = new List<DataColumn>();
                        var columnIndexMap = new Dictionary<int, int>(); // ממפה מאינדקס מקורי לאינדקס בפלט

                        for (int col = 0; col < data.Columns.Count; col++)
                        {
                            if (!_hiddenColumns.Contains(data.Columns[col].ColumnName))
                            {
                                visibleColumns.Add(data.Columns[col]);
                                columnIndexMap[col] = visibleColumns.Count - 1;
                            }
                        }

                        // הוספת כותרות בעברית (רק לעמודות גלויות)
                        for (int col = 0; col < visibleColumns.Count; col++)
                        {
                            string columnName = visibleColumns[col].ColumnName;
                            string hebrewHeader = GetHebrewColumnName(columnName);

                            worksheet.Cells[4, col + 1].Value = hebrewHeader;
                            worksheet.Cells[4, col + 1].Style.Font.Bold = true;
                            worksheet.Cells[4, col + 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                            worksheet.Cells[4, col + 1].Style.Fill.BackgroundColor.SetColor(Color.LightGray);
                            worksheet.Cells[4, col + 1].Style.Border.BorderAround(ExcelBorderStyle.Thin);
                        }

                        // הוספת נתונים
                        for (int row = 0; row < data.Rows.Count; row++)
                        {
                            // בדיקה אם זו שורת סיכום - פיצול התנאי לשלבים
                            bool isSummaryRow = CheckIfSummaryRow(data, row);

                            // הוספת התאים (רק לעמודות גלויות)
                            for (int colIdx = 0; colIdx < data.Columns.Count; colIdx++)
                            {
                                // דלג על עמודות מוסתרות
                                if (!columnIndexMap.ContainsKey(colIdx))
                                    continue;

                                int excelColIdx = columnIndexMap[colIdx] + 1;
                                var cell = worksheet.Cells[row + 5, excelColIdx];
                                var value = data.Rows[row][colIdx];

                                cell.Value = value;
                                cell.Style.Border.BorderAround(ExcelBorderStyle.Thin);

                                // עיצוב שורת סיכום
                                if (isSummaryRow)
                                {
                                    cell.Style.Font.Bold = true;
                                    cell.Style.Font.Color.SetColor(Color.FromArgb(0, 85, 170)); // כחול
                                    cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
                                    cell.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(240, 248, 255)); // רקע כחול בהיר
                                }

                                // התאמת סוג נתונים
                                if (value is decimal || value is double || value is float)
                                {
                                    cell.Style.Numberformat.Format = "#,##0.00";
                                    cell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
                                }
                                else if (value is DateTime)
                                {
                                    cell.Style.Numberformat.Format = "dd/MM/yyyy";
                                }
                            }
                        }

                        // התאמת רוחב עמודות
                        worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();
                    }

                    // רישום הצלחה
                    _errorManager.LogInfo(
                        ErrorCodes.Excel.Generation_Failed,
                        $"קובץ Excel נוצר בהצלחה עבור דוח {reportTitle}");

                    return package.GetAsByteArray();
                }
            }
            catch (Exception ex)
            {
                _errorManager.LogError(
                    ErrorCodes.Excel.Generation_Failed,
                    ErrorSeverity.Critical,
                    $"שגיאה ביצירת קובץ Excel עבור דוח {reportTitle}",
                    ex);
                throw new Exception($"Error generating Excel file: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// בודק אם השורה היא שורת סיכום
        /// </summary>
        private bool CheckIfSummaryRow(DataTable data, int rowIndex)
        {
            if (data.Columns.Contains("IsSummary") && data.Rows[rowIndex]["IsSummary"] != DBNull.Value)
            {
                var summaryValue = data.Rows[rowIndex]["IsSummary"];

                // בדיקה אם הערך הוא Boolean
                if (summaryValue is bool boolValue)
                {
                    return boolValue;
                }
                // בדיקה אם הערך הוא מספר (1 = אמת)
                else if (summaryValue is int intValue)
                {
                    return (intValue == 1);
                }
                // בדיקה אם הערך הוא מחרוזת
                else if (summaryValue is string strValue)
                {
                    if (bool.TryParse(strValue, out bool parsedBool))
                    {
                        return parsedBool;
                    }
                    else if (int.TryParse(strValue, out int parsedInt))
                    {
                        return (parsedInt == 1);
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// מקבל את השם העברי של עמודה מתוך מילון המיפויים
        /// </summary>
        /// <param name="columnName">שם העמודה באנגלית</param>
        /// <returns>שם עברי מהמיפוי, או שם העמודה המקורי אם אין מיפוי</returns>
        private string GetHebrewColumnName(string columnName)
        {
            if (_columnMappings.TryGetValue(columnName, out string hebrewName))
                return hebrewName;

            return columnName; // אם אין מיפוי, להחזיר את שם העמודה המקורי
        }
    }
}