using System;

namespace ReportGenerator.Core.Errors
{
    /// <summary>
    /// רמות חומרה לשגיאות
    /// </summary>
    public enum ErrorSeverity
    {

        /// מידע בלבד, לא מהווה שגיאה
        Information = 0,

        /// אזהרה, פעולה תקינה חלקית
        Warning = 1,

        /// שגיאה, אך אפשר להמשיך את התהליך
        Error = 2,

        /// שגיאה קריטית, יש להפסיק את התהליך
        Critical = 3
    }
}