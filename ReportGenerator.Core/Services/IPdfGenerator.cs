using ReportGenerator.Core.Data.Models;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

namespace ReportGenerator.Core.Services
{
    /// <summary>
    /// ממשק ליוצר קבצי PDF
    /// </summary>
    public interface IPdfGenerator
    {
        /// <summary>
        /// מייצר PDF מתבנית HTML
        /// </summary>
        /// <param name="templateName">שם התבנית</param>
        /// <param name="reportTitle">כותרת הדוח</param>
        /// <param name="dataTables">טבלאות נתונים</param>
        /// <param name="parameters">פרמטרים שהועברו לדוח</param>
        /// <returns>מערך בייטים של קובץ PDF</returns>
        Task<byte[]> GenerateFromTemplate(
            string templateName,
            string reportTitle,
            Dictionary<string, DataTable> dataTables,
            Dictionary<string, ParamValue> parameters);
    }
}