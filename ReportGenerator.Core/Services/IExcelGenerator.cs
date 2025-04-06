using System.Collections.Generic;
using System.Data;

namespace ReportGenerator.Core.Services
{
    /// <summary>
    /// ממשק ליוצר קבצי Excel
    /// </summary>
    public interface IExcelGenerator
    {
        /// <summary>
        /// יוצר קובץ אקסל ממספר טבלאות נתונים
        /// </summary>
        /// <param name="dataTables">מילון טבלאות נתונים</param>
        /// <param name="reportTitle">כותרת הדוח</param>
        /// <returns>מערך בייטים של קובץ אקסל</returns>
        byte[] Generate(Dictionary<string, DataTable> dataTables, string reportTitle);
    }
}