using System.Threading.Tasks;
using System.Collections.Generic;
using ReportGenerator.Core.Data.Models;
using ReportGenerator.Core.Management.Enums;

namespace ReportGenerator.Core.Services
{
    /// <summary>
    /// ממשק למנהל הדוחות המרכזי של המערכת
    /// </summary>
    public interface IReportManager
    {
        /// <summary>
        /// מפיק דוח לפי שם והפרמטרים שהתקבלו
        /// </summary>
        /// <param name="reportName">שם הדוח להפקה</param>
        /// <param name="format">פורמט הפלט (PDF או Excel)</param>
        /// <param name="parameters">פרמטרים להעברה לפרוצדורות</param>
        /// <returns>מערך בייטים של הקובץ המבוקש</returns>
        Task<byte[]> GenerateReport(string reportName, OutputFormat format, params object[] parameters);

        /// <summary>
        /// מקבל את הגדרות הדוח לפי שם
        /// </summary>
        /// <param name="reportName">שם הדוח</param>
        /// <returns>אובייקט הגדרות הדוח</returns>
        Task<ReportConfig> GetReportConfig(string reportName);

        /// <summary>
        /// מחזיר רשימת כל הדוחות הזמינים במערכת
        /// </summary>
        /// <returns>רשימת הגדרות דוחות</returns>
        Task<List<ReportConfig>> GetAvailableReports();
    }
}
