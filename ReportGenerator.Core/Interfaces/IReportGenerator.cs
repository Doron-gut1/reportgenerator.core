using System;
using System.Threading.Tasks;
using ReportGenerator.Core.Management.Enums;

namespace ReportGenerator.Core.Interfaces
{
    /// <summary>
    /// ממשק להפקת דוחות
    /// </summary>
    public interface IReportGenerator
    {
        /// <summary>
        /// מייצר דוח ומחזיר bytes בלבד - ללא שמירה לדיסק
        /// </summary>
        /// <param name="reportName">שם הדוח</param>
        /// <param name="format">פורמט הפלט (PDF/Excel)</param>
        /// <param name="parameters">פרמטרים לדוח</param>
        /// <returns>מערך בייטים של הקובץ המבוקש</returns>
        Task<byte[]> GenerateReportBytesOnly(string reportName, OutputFormat format, params object[] parameters);
        
        /// <summary>
        /// מייצר דוח, שומר לדיסק, ומחזיר bytes
        /// </summary>
        /// <param name="reportName">שם הדוח</param>
        /// <param name="format">פורמט הפלט (PDF/Excel)</param>
        /// <param name="savePath">נתיב שמירה מותאם (אופציונלי)</param>
        /// <param name="parameters">פרמטרים לדוח</param>
        /// <returns>מערך בייטים של הקובץ המבוקש</returns>
        Task<byte[]> GenerateReportAndSave(string reportName, OutputFormat format, string? savePath = null, params object[] parameters);

        /// <summary>
        /// מייצר דוח לפי שם, פורמט ופרמטרים
        /// הפונקציה הקיימת - נשמרת לתאימות אחורה
        /// מתנהגת לפי הגדרות הקונפיגורציה (SaveOutputFiles)
        /// </summary>
        /// <param name="reportName">שם הדוח</param>
        /// <param name="format">פורמט הפלט (PDF/Excel)</param>
        /// <param name="parameters">פרמטרים לדוח</param>
        /// <returns>מערך בייטים של הקובץ המבוקש</returns>
        [Obsolete("השתמש ב-GenerateReportBytesOnly או GenerateReportAndSave לבהירות מלאה")]
        Task<byte[]> GenerateReport(string reportName, OutputFormat format, params object[] parameters);

        /// <summary>
        /// מייצר דוח בצורה אסינכרונית ושומר אותו לקובץ
        /// </summary>
        /// <param name="reportName">שם הדוח</param>
        /// <param name="format">פורמט הפלט</param>
        /// <param name="parameters">פרמטרים</param>
        void GenerateReportAsync(string reportName, OutputFormat format, params object[] parameters);
    }
}
