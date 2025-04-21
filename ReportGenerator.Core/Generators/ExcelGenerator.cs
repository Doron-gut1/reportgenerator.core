using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using OfficeOpenXml;
using OfficeOpenXml.Drawing.Chart;
using OfficeOpenXml.Style;
using ReportGenerator.Core.Interfaces;

namespace ReportGenerator.Core.Generators
{
    public class ExcelGenerator : IExcelGenerator
    {
        private readonly Dictionary<string, string> _columnMappings;
        private readonly List<string> _hiddenColumns = new List<string> { "IsSummary" };

        /// <summary>
        /// יוצר מופע חדש של יוצר קבצי אקסל
        /// </summary>
        /// <param name="columnMappings">מילון מיפויים בין שמות עמודות באנגלית לעברית</param>
        public ExcelGenerator(Dictionary<string, string> columnMappings = null)
        {
            _columnMappings = columnMappings ?? new Dictionary<string, string>();
        }

        /// <summary>
        /// צבעים לעיצוב האקסל
        /// </summary>
        private static class ExcelColors
        {
            public static Color HeaderBackground = Color.FromArgb(79, 129, 189); // כחול כהה
            public static Color HeaderText = Color.White;
            public static Color SummaryBackground = Color.FromArgb(255, 242, 204); // צהוב בהיר
            public static Color SummaryText = Color.FromArgb(156, 87, 0); // חום
            public static Color GridLines = Color.FromArgb(217, 217, 217); // אפור בהיר
        }

        /// <summary>
        /// יוצר קובץ אקסל עם חישובי סיכום וסטטיסטיקה מתקדמים
        /// </summary>
        /// <param name="dataTables">מילון טבלאות נתונים</param>
        /// <param name="reportTitle">כותרת הדוח</param>
        /// <param name="addStatistics">האם להוסיף סטטיסטיקה מפורטת</param>
        /// <param name="statisticsSheetName">שם גיליון הסטטיסטיקה</param>
        /// <returns>מערך בייטים של קובץ אקסל</returns>
        public byte[] GenerateWithStatistics(Dictionary<string, DataTable> dataTables, string reportTitle, bool addStatistics = true, string statisticsSheetName = "סטטיסטיקה")
        {
            if (dataTables == null || dataTables.Count == 0)
                throw new ArgumentException("No data provided for Excel generation");

            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            using (var package = new ExcelPackage())
            {
                // יצירת הגיליונות הרגילים
                foreach (var tableEntry in dataTables)
                {
                    string tableName = tableEntry.Key;
                    DataTable data = tableEntry.Value;

                    if (data.Rows.Count == 0)
                        continue;

                    // שימוש במתודת CreateDataSheet לכל אחד מגיליונות הנתונים
                    CreateDataSheet(package, tableName, data, reportTitle);
                }

                // אם נדרש, הוספת גיליון סטטיסטיקה
                if (addStatistics)
                {
                    CreateStatisticsSheet(package, dataTables, reportTitle, statisticsSheetName);
                }

                return package.GetAsByteArray();
            }
        }

        /// <summary>
        /// יוצר גיליון סטטיסטיקה מורחב שמכיל אנליזה ויזואלית של הנתונים
        /// </summary>
        private void CreateStatisticsSheet(ExcelPackage package, Dictionary<string, DataTable> dataTables, string reportTitle, string sheetName)
        {
            // יצירת גיליון הסטטיסטיקה
            var worksheet = package.Workbook.Worksheets.Add(sheetName);
            worksheet.View.RightToLeft = true;

            // הגדרת כותרת ראשית
            worksheet.Cells[1, 1].Value = $"ניתוח סטטיסטי - {reportTitle}";
            worksheet.Cells[1, 1, 1, 8].Merge = true;
            worksheet.Cells[1, 1].Style.Font.Bold = true;
            worksheet.Cells[1, 1].Style.Font.Size = 16;
            worksheet.Cells[1, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            worksheet.Cells[1, 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
            worksheet.Cells[1, 1].Style.Fill.BackgroundColor.SetColor(ExcelColors.HeaderBackground);
            worksheet.Cells[1, 1].Style.Font.Color.SetColor(ExcelColors.HeaderText);
            worksheet.Cells[1, 1].Style.Border.BorderAround(ExcelBorderStyle.Medium);
            
            // הוספת תאריך ושעה
            worksheet.Cells[2, 1].Value = $"הופק בתאריך: {DateTime.Now:dd/MM/yyyy} {DateTime.Now:HH:mm}";
            worksheet.Cells[2, 1, 2, 8].Merge = true;
            worksheet.Cells[2, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            worksheet.Cells[2, 1].Style.Font.Bold = true;

            int currentRow = 4;
            int startRow = currentRow;
            
            // יצירת תוכן עניינים
            worksheet.Cells[currentRow, 1].Value = "תוכן עניינים:";
            worksheet.Cells[currentRow, 1].Style.Font.Bold = true;
            worksheet.Cells[currentRow, 1].Style.Font.Size = 12;
            currentRow += 1;
            
            int tocStartRow = currentRow;
            
            // שמירת מיקום התחלת תוכן העניינים לצורך מילוי מאוחר יותר
            int tableIndex = 1;
            currentRow += dataTables.Count + 2; // שמירת מקום לתוכן העניינים

            // מילון למעקב אחרי שורות התחלה של כל טבלה עבור הקישורים
            var tableStartRows = new Dictionary<string, int>();

            // יצירת גרף עוגה עבור הטבלה הראשונה (אם יש נתונים מספריים)
            bool createdSummaryChart = false;

            // לולאה על כל טבלאות הנתונים
            foreach (var tableEntry in dataTables)
            {
                string tableName = tableEntry.Key;
                DataTable data = tableEntry.Value;

                if (data.Rows.Count == 0)
                    continue;

                // שמירת מיקום התחלת הטבלה עבור קישורים בתוכן העניינים
                tableStartRows[tableName] = currentRow;
                
                // הוספת כותרת משנה עם מספור הטבלה
                worksheet.Cells[currentRow, 1].Value = $"{tableIndex}. ניתוח נתונים: {tableName}";
                worksheet.Cells[currentRow, 1, currentRow, 8].Merge = true;
                worksheet.Cells[currentRow, 1].Style.Font.Bold = true;
                worksheet.Cells[currentRow, 1].Style.Font.Size = 14;
                worksheet.Cells[currentRow, 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                worksheet.Cells[currentRow, 1].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(230, 230, 230));
                worksheet.Cells[currentRow, 1].Style.Border.BorderAround(ExcelBorderStyle.Thin);
                currentRow += 2;

                // כותרות לסטטיסטיקה
                worksheet.Cells[currentRow, 1].Value = "שם השדה";
                worksheet.Cells[currentRow, 2].Value = "מספר שורות";
                worksheet.Cells[currentRow, 3].Value = "סכום כולל";
                worksheet.Cells[currentRow, 4].Value = "מינימום";
                worksheet.Cells[currentRow, 5].Value = "מקסימום";
                worksheet.Cells[currentRow, 6].Value = "ממוצע";
                worksheet.Cells[currentRow, 7].Value = "חציון";
                worksheet.Cells[currentRow, 8].Value = "סטיית תקן";

                // עיצוב הכותרות
                var headerRange = worksheet.Cells[currentRow, 1, currentRow, 8];
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
                headerRange.Style.Fill.BackgroundColor.SetColor(ExcelColors.HeaderBackground);
                headerRange.Style.Font.Color.SetColor(ExcelColors.HeaderText);
                headerRange.Style.Border.BorderAround(ExcelBorderStyle.Thin);
                headerRange.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                currentRow++;

                // נתוני העמודה הראשונה שיש לה נתונים מספריים (עבור הגרף)
                DataColumn? firstNumericColumn = null;
                int numericFieldCount = 0;
                int statStartRow = currentRow;
                
                // לולאה על כל העמודות
                foreach (DataColumn column in data.Columns)
                {
                    if (_hiddenColumns.Contains(column.ColumnName))
                        continue;
                    
                    // קבלת שם בעברית
                    string hebrewName = GetHebrewColumnName(column.ColumnName);
                    worksheet.Cells[currentRow, 1].Value = hebrewName;
                    worksheet.Cells[currentRow, 1].Style.Font.Bold = true;
                    
                    bool isNumeric = column.DataType == typeof(int) || column.DataType == typeof(double) || 
                                   column.DataType == typeof(decimal) || column.DataType == typeof(float);
                    
                    // אם זה שדה מספרי, חשב סטטיסטיקה
                    if (isNumeric)
                    {
                        // שמירת העמודה הראשונה המספרית לצורך יצירת גרף
                        if (firstNumericColumn == null)
                            firstNumericColumn = column;
                        
                        numericFieldCount++;
                        
                        // חישוב סטטיסטיקה
                        decimal min = decimal.MaxValue;
                        decimal max = decimal.MinValue;
                        decimal sum = 0;
                        int count = 0;
                        List<decimal> values = new List<decimal>();

                        // חישוב ערכים סטטיסטיים
                        foreach (DataRow row in data.Rows)
                        {
                            if (row[column] != DBNull.Value && decimal.TryParse(row[column].ToString(), out decimal value))
                            {
                                min = Math.Min(min, value);
                                max = Math.Max(max, value);
                                sum += value;
                                count++;
                                values.Add(value);
                            }
                        }

                        // חישוב סטיית תקן וחציון
                        decimal avg = count > 0 ? sum / count : 0;
                        decimal variance = 0;
                        decimal median = 0;

                        if (count > 0)
                        {
                            // חישוב שונות
                            foreach (decimal value in values)
                            {
                                variance += (value - avg) * (value - avg);
                            }
                            variance /= count;
                            
                            // חישוב חציון
                            values.Sort();
                            if (values.Count % 2 == 0)
                                median = (values[values.Count / 2] + values[values.Count / 2 - 1]) / 2;
                            else
                                median = values[values.Count / 2];
                        }

                        decimal stdDev = (decimal)Math.Sqrt((double)variance);

                        // הצגת הסטטיסטיקה
                        worksheet.Cells[currentRow, 2].Value = count;
                        worksheet.Cells[currentRow, 3].Value = count > 0 ? (object)sum : "N/A";
                        worksheet.Cells[currentRow, 4].Value = count > 0 ? (object)min : "N/A";
                        worksheet.Cells[currentRow, 5].Value = count > 0 ? (object)max : "N/A";
                        worksheet.Cells[currentRow, 6].Value = count > 0 ? (object)avg : "N/A";
                        worksheet.Cells[currentRow, 7].Value = count > 0 ? (object)median : "N/A";
                        worksheet.Cells[currentRow, 8].Value = count > 0 ? (object)stdDev : "N/A";

                        // עיצוב תאים מספריים
                        worksheet.Cells[currentRow, 3, currentRow, 8].Style.Numberformat.Format = "#,##0.00";
                        worksheet.Cells[currentRow, 3, currentRow, 8].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
                        worksheet.Cells[currentRow, 2].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    }
                    else
                    {
                        // אם לא מספרי, הצג רק מספר ערכים
                        int count = data.Rows.Count;
                        int distinctValues = data.AsEnumerable().Select(r => r[column].ToString()).Distinct().Count();
                        worksheet.Cells[currentRow, 2].Value = count;
                        worksheet.Cells[currentRow, 3].Value = $"{distinctValues} ערכים ייחודיים";
                        worksheet.Cells[currentRow, 2].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                        
                        // מיזוג תאים ריקים
                        worksheet.Cells[currentRow, 4, currentRow, 8].Merge = true;
                        worksheet.Cells[currentRow, 4].Value = "לא ישים לשדה טקסטואלי";
                        worksheet.Cells[currentRow, 4].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                        worksheet.Cells[currentRow, 4].Style.Font.Italic = true;
                        worksheet.Cells[currentRow, 4].Style.Font.Color.SetColor(Color.Gray);
                    }
                    
                    // הוספת מסגרת סביב השורה
                    worksheet.Cells[currentRow, 1, currentRow, 8].Style.Border.BorderAround(ExcelBorderStyle.Thin, Color.LightGray);
                    worksheet.Cells[currentRow, 1, currentRow, 8].Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
                    worksheet.Cells[currentRow, 1, currentRow, 8].Style.Border.Bottom.Color.SetColor(Color.LightGray);
                    
                    // צביעת רקע לסירוגין
                    if ((currentRow - statStartRow) % 2 == 0)
                    {
                        worksheet.Cells[currentRow, 1, currentRow, 8].Style.Fill.PatternType = ExcelFillStyle.Solid;
                        worksheet.Cells[currentRow, 1, currentRow, 8].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(245, 245, 245));
                    }
                    
                    currentRow++;
                }
                
                // יצירת גרף עוגה אם יש עמודה מספרית ואם עוד לא יצרנו גרף
                if (firstNumericColumn != null && !createdSummaryChart)
                {   
                    // בחר רק 5-10 שורות ראשונות או שורות סיכום לגרף
                    int maxRowsForChart = Math.Min(data.Rows.Count, 10);
                    List<DataRow> rowsForChart = new List<DataRow>();
                    
                    // ניסיון למצוא שורות סיכום
                    bool foundSummaryRows = false;
                    if (data.Columns.Contains("IsSummary") || data.Columns.Contains("hesder"))
                    {
                        foreach (DataRow row in data.Rows)
                        {
                            // בדיקה אם זו שורת סיכום
                            bool isSummary = false;
                            
                            if (data.Columns.Contains("IsSummary") && row["IsSummary"] != DBNull.Value)
                            {
                                var summaryValue = row["IsSummary"];
                                isSummary = summaryValue is bool boolVal && boolVal ||
                                          summaryValue is int intVal && (intVal == 1 || intVal == -1) ||
                                          summaryValue is string strVal && (strVal == "1" || strVal == "-1" || strVal.ToLower() == "true");
                            }                
                            if (isSummary)
                            {
                                rowsForChart.Add(row);
                                foundSummaryRows = true;
                            }
                        }
                    }
                    
                    // אם לא נמצאו שורות סיכום, השתמש ב-N שורות ראשונות
                    if (!foundSummaryRows)
                    {
                        for (int i = 0; i < maxRowsForChart; i++)
                        {
                            rowsForChart.Add(data.Rows[i]);
                        }
                    }
                    
                    // הוספת כותרת עבור הגרף
                    currentRow += 2;
                    worksheet.Cells[currentRow, 1].Value = $"גרף ויזואלי - {GetHebrewColumnName(firstNumericColumn.ColumnName)}";
                    worksheet.Cells[currentRow, 1, currentRow, 8].Merge = true;
                    worksheet.Cells[currentRow, 1].Style.Font.Bold = true;
                    worksheet.Cells[currentRow, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    currentRow++;
                    
                    // יצירת הגרף
                    string labelColumnName = data.Columns[0].ColumnName; // שימוש בעמודה הראשונה לתוויות
                    string valueColumnName = firstNumericColumn.ColumnName;
                    
                    var chart = worksheet.Drawings.AddChart("Chart1", OfficeOpenXml.Drawing.Chart.eChartType.Pie);
                    chart.SetPosition(currentRow, 0, 1, 0);
                    chart.SetSize(800, 400);
                    
                    // יצירת סדרת נתונים
                    var series = chart.Series.Add($"='{"+sheetName+"}'!$A$7:$A$16", $"='{"+sheetName+"}'!$B$7:$B$16");
                    
                    // מילוי נתונים לגרף
                    int chartRow = currentRow + 2;
                    for (int i = 0; i < rowsForChart.Count; i++)
                    {
                        var row = rowsForChart[i];
                        string label = row[labelColumnName].ToString();
                        string hebrewLabel = GetHebrewColumnName(label);
                        object value = row[valueColumnName];
                        
                        // הוספת התווית והערך בגיליון עבור הגרף
                        worksheet.Cells[chartRow + i, 1].Value = string.IsNullOrEmpty(hebrewLabel) ? label : hebrewLabel;
                        worksheet.Cells[chartRow + i, 2].Value = value;
                    }

                    // הגדרות נוספות לגרף
                    var pieChart = (ExcelPieChart)chart;
                    pieChart.DataLabel.ShowPercent = true;
                    pieChart.DataLabel.ShowValue = true;
                    pieChart.DataLabel.ShowCategory = true;

                    pieChart.Title.Text = $"התפלגות {GetHebrewColumnName(valueColumnName)} לפי {GetHebrewColumnName(labelColumnName)}";
                    pieChart.Legend.Position = OfficeOpenXml.Drawing.Chart.eLegendPosition.Bottom;

                    // התקדמות בשורות להמשך הדוח
                    currentRow += rowsForChart.Count + 15;
                    createdSummaryChart = true;
                }
                else
                {
                    // רק רווח גדול יותר בין הטבלאות
                    currentRow += 3;
                }
                
                // הוספת גרף עמודות אם יש לפחות 2 שדות מספריים
                if (numericFieldCount >= 2 && data.Rows.Count > 0)
                {
                    worksheet.Cells[currentRow, 1].Value = "השוואת שדות מספריים";
                    worksheet.Cells[currentRow, 1, currentRow, 8].Merge = true;
                    worksheet.Cells[currentRow, 1].Style.Font.Bold = true;
                    worksheet.Cells[currentRow, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    currentRow++;
                    
                    // יצירת גרף עמודות להשוואת הממוצעים
                    var barChart = worksheet.Drawings.AddChart($"BarChart{tableIndex}", OfficeOpenXml.Drawing.Chart.eChartType.ColumnClustered);
                    barChart.SetPosition(currentRow, 0, 2, 0);
                    barChart.SetSize(800, 400);
                    barChart.Title.Text = "השוואת ערכים ממוצעים לפי שדה";
                    
                    // נוריד את הגרף אם אין מספיק נתונים
                    if (numericFieldCount < 2)
                    {
                        worksheet.Drawings.Remove(barChart);
                    }
                    
                    currentRow += 18;
                }
                
                tableIndex++;
            }
            
            // השלמת תוכן העניינים
            int tocRow = tocStartRow;
            tableIndex = 1;
            foreach (var entry in tableStartRows)
            {
                worksheet.Cells[tocRow, 1].Value = $"{tableIndex}. {entry.Key}";
                var hyperlink = worksheet.Cells[tocRow, 1].Hyperlink = new ExcelHyperLink($"#{entry.Value}!A1", entry.Key);
                worksheet.Cells[tocRow, 1].Style.Font.UnderLine = true;
                worksheet.Cells[tocRow, 1].Style.Font.Color.SetColor(Color.Blue);
                tocRow++;
                tableIndex++;
            }
            
            // עיצוב סופי
            worksheet.Column(1).Width = 30; // עמודת שמות שדות רחבה יותר
            worksheet.Column(2).Width = 15;
            worksheet.Column(3).Width = 20;
            worksheet.Column(4).Width = 15;
            worksheet.Column(5).Width = 15;
            worksheet.Column(6).Width = 15;
            worksheet.Column(7).Width = 15;
            worksheet.Column(8).Width = 15;
            
            // הגדרת קפיאת כותרות
            worksheet.View.FreezePanes(startRow + dataTables.Count + 4, 1);
            
            // הוספת הערות שימוש בתחתית הדף
            int notesRow = worksheet.Dimension.End.Row + 3;
            worksheet.Cells[notesRow, 1].Value = "הערות שימוש:";
            worksheet.Cells[notesRow, 1].Style.Font.Bold = true;
            worksheet.Cells[notesRow + 1, 1].Value = "1. ניתן ללחוץ על הקישורים בתוכן העניינים כדי לנווט לטבלאות השונות";
            worksheet.Cells[notesRow + 2, 1].Value = "2. ברשימת הסטטיסטיקה מוצגים ערכים מסכמים בלבד. לקבלת הנתונים המפורטים, יש לעיין בגיליונות הנתונים";
            worksheet.Cells[notesRow + 3, 1].Value = $"3. דוח זה הופק אוטומטית על ידי מערכת הדוחות ב- {DateTime.Now:dd/MM/yyyy HH:mm}";
        }

        /// <summary>
        /// יוצר גיליון נתונים רגיל
        /// </summary>
        private void CreateDataSheet(ExcelPackage package, string tableName, DataTable data, string reportTitle)
        {
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
                var headerCell = worksheet.Cells[4, col + 1];
                
                // עיצוב משופר לכותרות
                headerCell.Style.Font.Bold = true;
                headerCell.Style.Font.Color.SetColor(ExcelColors.HeaderText);
                headerCell.Style.Fill.PatternType = ExcelFillStyle.Solid;
                headerCell.Style.Fill.BackgroundColor.SetColor(ExcelColors.HeaderBackground);
                headerCell.Style.Border.BorderAround(ExcelBorderStyle.Thin);
                headerCell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                headerCell.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
            }

            // הוספת נתונים
            for (int row = 0; row < data.Rows.Count; row++)
            {
                // בדיקה אם זו שורת סיכום
                bool isSummaryRow = false;

                // בדיקה אם יש עמודת IsSummary מפורשת
                if (data.Columns.Contains("IsSummary") && data.Rows[row]["IsSummary"] != DBNull.Value)
                {
                    var summaryValue = data.Rows[row]["IsSummary"];

                    // בדיקה אם הערך הוא Boolean
                    if (summaryValue is bool boolValue)
                    {
                        isSummaryRow = boolValue;
                    }
                    // בדיקה אם הערך הוא מספר (1 = אמת, -1 = סיכום)
                    else if (summaryValue is int intValue)
                    {
                        isSummaryRow = (intValue == 1 || intValue == -1);
                    }
                    // בדיקה אם הערך הוא מחרוזת
                    else if (summaryValue is string strValue)
                    {
                        if (bool.TryParse(strValue, out bool parsedBool))
                        {
                            isSummaryRow = parsedBool;
                        }
                        else if (int.TryParse(strValue, out int parsedInt))
                        {
                            isSummaryRow = (parsedInt == 1 || parsedInt == -1);
                        }
                    }
                }
                
                // בדיקה אלטרנטיבית לפי מאפיינים ידועים של שורת סיכום
                if (!isSummaryRow)
                {
                    // בדיקה אם יש עמודה 'hesder' עם ערך -1
                    if (data.Columns.Contains("hesder") && data.Rows[row]["hesder"] != DBNull.Value)
                    {
                        var hesderValue = data.Rows[row]["hesder"];
                        if (hesderValue is int hesderInt && hesderInt == -1)
                        {
                            isSummaryRow = true;
                        }
                        else if (hesderValue is string hesderStr && hesderStr == "-1")
                        {
                            isSummaryRow = true;
                        }
                    }
                }

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
                        // עיצוב משופר לשורות סיכום עם צבעים שונים
                        cell.Style.Font.Bold = true;
                        cell.Style.Font.Color.SetColor(ExcelColors.SummaryText);
                        cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
                        cell.Style.Fill.BackgroundColor.SetColor(ExcelColors.SummaryBackground);
                        cell.Style.Border.BorderAround(ExcelBorderStyle.Medium, ExcelColors.SummaryText);
                        
                        // הדגשת תאי סכום
                        if (value is decimal || value is double || value is float)
                        {
                            cell.Style.Font.Size = 11f;  // גודל גופן מוגדל מעט
                        }
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
            
            // הגדרת קפיאת כותרות (freeze panes)
            worksheet.View.FreezePanes(5, 1);  // קפיאת שורות 1-4 למעלה
            
            // הגדרת טבלת אקסל עם יכולות סינון
            if (data.Rows.Count > 0)
            {
                int dataRows = data.Rows.Count;
                int dataCols = visibleColumns.Count;
                
                // יצירת טבלת אקסל עם יכולות סינון
                if (dataRows > 0 && dataCols > 0)
                {
                    try
                    {
                        var tableRange = worksheet.Cells[4, 1, dataRows + 4, dataCols];
                        var table = worksheet.Tables.Add(tableRange, $"Table_{tableName.Replace(" ", "_")}");
                        table.ShowHeader = true;
                        table.TableStyle = OfficeOpenXml.Table.TableStyles.Medium2;
                    }
                    catch (Exception) 
                    {
                        // במקרה של שגיאה ביצירת הטבלה, נמשיך בלעדיה
                    }
                }
            }
        }
        public byte[] Generate(Dictionary<string, DataTable> dataTables, string reportTitle)
        {
            if (dataTables == null || dataTables.Count == 0)
                throw new ArgumentException("No data provided for Excel generation");

            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            using (var package = new ExcelPackage())
            {
                // יצירת גיליונות עבור כל טבלאות הנתונים
                foreach (var tableEntry in dataTables)
                {
                    string tableName = tableEntry.Key;
                    DataTable data = tableEntry.Value;

                    if (data.Rows.Count == 0)
                        continue;

                    // שימוש במתודת CreateDataSheet שמייצרת את הגיליון ומעצבת אותו
                    CreateDataSheet(package, tableName, data, reportTitle);
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
            // בדיקה אם יש מיפוי ישיר
            if (_columnMappings.TryGetValue(columnName, out string hebrewName))
                return hebrewName;
            
            // טיפול בשדות מסוג TableName_ColumnName
            int underscoreIndex = columnName.IndexOf('_');
            if (underscoreIndex > 0)
            {
                string tableName = columnName.Substring(0, underscoreIndex);
                string fieldName = columnName.Substring(underscoreIndex + 1);
                
                // חיפוש לפי המבנה המורכב
                string compositeKey = $"{tableName}_{fieldName}";
                if (_columnMappings.TryGetValue(compositeKey, out string mappedName))
                    return mappedName;
            }
            
            return columnName; // ברירת מחדל אם אין מיפוי
        }
    }
}