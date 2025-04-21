using System.Collections.Generic;
using System.Data;

namespace ReportGenerator.Core.Interfaces
{
    /// <summary>
    /// ממשק ליצירת קבצי Excel
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

        /// <summary>
        /// יוצר קובץ אקסל עם חישובי סיכום וסטטיסטיקה מתקדמים
        /// </summary>
        /// <param name="dataTables">מילון טבלאות נתונים</param>
        /// <param name="reportTitle">כותרת הדוח</param>
        /// <param name="addStatistics">האם להוסיף סטטיסטיקה מפורטת</param>
        /// <param name="statisticsSheetName">שם גיליון הסטטיסטיקה</param>
        /// <returns>מערך בייטים של קובץ אקסל</returns>
        byte[] GenerateWithStatistics(Dictionary<string, DataTable> dataTables, string reportTitle, bool addStatistics = true, string statisticsSheetName = "סטטיסטיקה");
    }
}
