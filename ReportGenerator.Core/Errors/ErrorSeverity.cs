using System;

namespace ReportGenerator.Core.Errors
{
    /// <summary>
    /// רמות חומרה לשגיאות
    /// </summary>
    public enum ErrorSeverity
    {
        /// <summary>
        /// מידע בלבד, לא מהווה שגיאה
        /// </summary>
        Information = 0,

        /// <summary>
        /// אזהרה, פעולה תקינה חלקית
        /// </summary>
        Warning = 1,

        /// <summary>
        /// שגיאה, אך אפשר להמשיך את התהליך
        /// </summary>
        Error = 2,

        /// <summary>
        /// שגיאה קריטית, יש להפסיק את התהליך
        /// </summary>
        Critical = 3
    }
}