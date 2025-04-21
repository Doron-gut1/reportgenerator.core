using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Diagnostics;
using ReportGenerator.Core.Data.Models;
using ReportGenerator.Core.Errors;
using ReportGenerator.Core.Interfaces;
using ReportGenerator.Core.Management.Enums;

namespace ReportGenerator.Core.Management.Services
{
    /// <summary>
    /// מחלקה לביצוע הפקת דוחות - אחראית על התהליך המלא מרגע קבלת פרמטרים ועד קבלת התוצאה
    /// </summary>
    internal class ReportExecutor
    {
        private readonly IDataAccess _dataAccess;
        private readonly IErrorManager _errorManager;

        /// <summary>
        /// יוצר מבצע דוחות חדש
        /// </summary>
        public ReportExecutor(IDataAccess dataAccess, IErrorManager errorManager)
        {
            _dataAccess = dataAccess ?? throw new ArgumentNullException(nameof(dataAccess));
            _errorManager = errorManager ?? throw new ArgumentNullException(nameof(errorManager));
        }

        /// <summary>
        /// מבצע תהליך מלא של הפקת דוח - מקבלת פרמטרים ועד הרצת הפרוצדורות והחזרת התוצאות
        /// </summary>
        /// <param name="reportName">שם הדוח</param>
        /// <param name="parsedParams">פרמטרים מעובדים</param>
        /// <returns>תצורת הדוח ונתוני הדוח</returns>
        public async Task<(ReportConfig Config, Dictionary<string, System.Data.DataTable> DataTables)> ExecuteReport(
            string reportName, 
            Dictionary<string, ParamValue> parsedParams)
        {
            try
            {
                // מדידת זמן ביצוע
                var stopwatch = Stopwatch.StartNew();

                // 1. קבלת הגדרות הדוח
                var reportConfig = await _dataAccess.GetReportConfig(reportName);
                
                // 2. הרצת כל הפרוצדורות
                var dataTables = await _dataAccess.ExecuteMultipleStoredProcedures(
                    reportConfig.StoredProcName, 
                    parsedParams);
                
                stopwatch.Stop();
                
                // רישום מידע על זמן הרצת הפרוצדורות
                _errorManager.LogInfo(
                    ErrorCode.General_Info,
                    $"פרוצדורות הדוח {reportName} הסתיימו בהצלחה. זמן ריצה: {stopwatch.ElapsedMilliseconds} מילישניות.",
                    reportName: reportName);

                // החזרת תצורת הדוח והנתונים שהתקבלו
                return (reportConfig, dataTables);
            }
            catch (Exception ex)
            {
                _errorManager.LogCriticalError(
                    ErrorCode.Report_Data_Retrieval_Failed,
                    $"שגיאה בקבלת נתוני דוח {reportName}",
                    ex,
                    reportName: reportName);
                
                throw new Exception($"Error retrieving report data for {reportName}: {ex.Message}", ex);
            }
        }
    }
}