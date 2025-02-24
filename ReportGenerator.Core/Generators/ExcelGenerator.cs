using System;
using System.Data;
using System.IO;
using OfficeOpenXml;

namespace ReportGenerator.Core.Generators
{
    public class ExcelGenerator
    {
        public byte[] Generate(DataTable data)
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            using (var package = new ExcelPackage())
            {
                var worksheet = package.Workbook.Worksheets.Add("Report");

                // הוספת כותרות
                for (int i = 0; i < data.Columns.Count; i++)
                {
                    worksheet.Cells[1, i + 1].Value = data.Columns[i].ColumnName;
                    worksheet.Cells[1, i + 1].Style.Font.Bold = true;
                }

                // הוספת נתונים
                for (int row = 0; row < data.Rows.Count; row++)
                {
                    for (int col = 0; col < data.Columns.Count; col++)
                    {
                        worksheet.Cells[row + 2, col + 1].Value = data.Rows[row][col];
                    }
                }

                // עיצוב אוטומטי של רוחב עמודות
                worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();

                return package.GetAsByteArray();
            }
        }
    }
}