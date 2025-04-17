using System.Data;
using System.Collections.Generic;
using System.Threading.Tasks;
using ReportGenerator.Core.Data.Models;

namespace ReportGenerator.Core.Interfaces
{
    /// <summary>
    /// ממשק לגישה לבסיס הנתונים
    /// </summary>
    public interface IDataAccess
    {
        /// <summary>
        /// מקבל את הגדרות הדוח
        /// </summary>
        Task<ReportConfig> GetReportConfig(string reportName);

        /// <summary>
        /// מקבל מיפויים של שמות עמודות לכותרות בעברית
        /// </summary>
        Task<Dictionary<string, string>> GetColumnMappings(string procNames);

        /// <summary>
        /// הרצת מספר פרוצדורות או פונקציות טבלאיות ומיזוג התוצאות
        /// </summary>
        Task<Dictionary<string, DataTable>> ExecuteMultipleStoredProcedures(
            string objectNames,
            Dictionary<string, ParamValue> parameters);

        /// <summary>
        /// מקבל את שם החודש לפי מספר חודש
        /// </summary>
        Task<string> GetMonthName(int mnt);

        /// <summary>
        /// מקבל את שם התקופה לפי מספר חודש
        /// </summary>
        Task<string> GetPeriodName(int mnt);

        /// <summary>
        /// מקבל את שם המועצה
        /// </summary>
        Task<string> GetMoazaName();

        /// <summary>
        /// מקבל את שם סוג החיוב לפי קוד
        /// </summary>
        Task<string> GetSugtsName(int sugts);

        /// <summary>
        /// מקבל את שם היישוב לפי קוד
        /// </summary>
        Task<string> GetIshvName(int isvkod);

        /// <summary>
        /// מקבל מידע על פרמטרים של פרוצדורה מאוחסנת
        /// </summary>
        Task<List<ParameterInfo>> GetProcedureParameters(string procName);
    }
}
