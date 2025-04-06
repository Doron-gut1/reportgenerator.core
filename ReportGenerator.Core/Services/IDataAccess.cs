using ReportGenerator.Core.Data.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

namespace ReportGenerator.Core.Services
{
    /// <summary>
    /// ממשק לגישה לנתונים
    /// </summary>
    public interface IDataAccess
    {
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
        /// מאפשר שליפת שמות מרובים לקודים (למקרה של רשימות)
        /// </summary>
        Task<string> GetCodeNames(string codes, string tableName, string codeField, string nameField);

        /// <summary>
        /// מקבל את הגדרות הדוח
        /// </summary>
        Task<ReportConfig> GetReportConfig(string reportName);

        /// <summary>
        /// בודק אם אובייקט SQL הוא פונקציה טבלאית
        /// </summary>
        Task<bool> IsTableFunction(string objectName);

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
        /// הרצת פונקציה טבלאית
        /// </summary>
        Task<DataTable> ExecuteTableFunction(string functionName, Dictionary<string, ParamValue> parameters);
    }
}