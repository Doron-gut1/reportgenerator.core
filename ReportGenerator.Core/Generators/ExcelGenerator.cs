using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using OfficeOpenXml;
using OfficeOpenXml.Style;

namespace ReportGenerator.Core.Generators
{
    public class ExcelGenerator
    {
        private readonly Dictionary<string, string> _columnMappings;

        /// <summary>
        /// יוצר מופע חדש של יוצר קבצי אקסל
        /// </summary>
        /// <param name="columnMappings">מילון מיפויים בין שמות עמודות באנגלית לעברית</param>
        public ExcelGenerator(Dictionary<string, string> columnMappings = null)
        {
            _columnMappings = columnMappings ?? new Dictionary<string, string>();
        }

        /// <summary>
        /// יוצר קובץ אקסל מטבלת נתונים
        /// </summary>
        /// <param name="data">טבלת נתונים מקורית</param>
        /// <returns>מערך בייטים של קובץ אקסל</returns>
        public byte[] Generate(DataTable data)
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            using (var package = new ExcelPackage())
            {
                var worksheet = package.Workbook.Worksheets.Add("Report");
                
                // הגדרת כיווניות לעברית
                worksheet.View.RightToLeft = true;

                // הוספת כותרות בעברית
                for (int i = 0; i < data.Columns.Count; i++)
                {
                    string columnName = data.Columns[i].ColumnName;
                    string hebrewName = GetHebrewColumnName(columnName);
                    
                    worksheet.Cells[1, i + 1].Value = hebrewName;
                    worksheet.Cells[1, i + 1].Style.Font.Bold = true;
                    worksheet.Cells[1, i + 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    worksheet.Cells[1, i + 1].Style.Fill.BackgroundColor.SetColor(Color.LightGray);
                }

                // הוספת נתונים
                for (int row = 0; row < data.Rows.Count; row++)
                {
                    for (int col = 0; col < data.Columns.Count; col++)
                    {
                        var cell = worksheet.Cells[row + 2, col + 1];
                        var value = data.Rows[row][col];
                        
                        cell.Value = value;
                        
                        // התאמת פורמט לסוג הנתונים
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

                // עיצוב אוטומטי של רוחב עמודות
                worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();

                return package.GetAsByteArray();
            }
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
                throw new ArgumentException("No data provided for Excel generation");
            
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            
            using (var package = new ExcelPackage())
            {
                foreach (var tableEntry in dataTables)
                {
                    string tableName = tableEntry.Key;
                    DataTable data = tableEntry.Value;
                    
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
                    
                    // הוספת כותרות בעברית
                    for (int col = 0; col < data.Columns.Count; col++)
                    {
                        string columnName = data.Columns[col].ColumnName;
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
                        for (int col = 0; col < data.Columns.Count; col++)
                        {
                            var cell = worksheet.Cells[row + 5, col + 1];
                            var value = data.Rows[row][col];
                            
                            cell.Value = value;
                            
                            // עיצוב תא
                            cell.Style.Border.BorderAround(ExcelBorderStyle.Thin);
                            
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
                
                return package.GetAsByteArray();
            }
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
