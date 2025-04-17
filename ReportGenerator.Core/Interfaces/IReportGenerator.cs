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
        /// מייצר דוח לפי שם, פורמט ופרמטרים
        /// </summary>
        /// <param name="reportName">שם הדוח</param>
        /// <param name="format">פורמט הפלט (PDF/Excel)</param>
        /// <param name="parameters">פרמטרים לדוח</param>
        /// <returns>מערך בייטים של הקובץ המבוקש</returns>
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
