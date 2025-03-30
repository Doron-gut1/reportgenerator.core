namespace ReportGenerator.Core.Data.Models
{
    /// <summary>
    /// מודל המייצג מיפוי בין שמות עמודות באנגלית לעברית
    /// </summary>
    public class ColumnMapping
    {
        /// <summary>
        /// שם הטבלה הלוגי
        /// </summary>
        public string TableName { get; set; }

        /// <summary>
        /// שם העמודה באנגלית
        /// </summary>
        public string ColumnName { get; set; }

        /// <summary>
        /// הכותרת בעברית (גנרי)
        /// </summary>
        public string HebrewAlias { get; set; }

        /// <summary>
        /// שם פרוצדורה ספציפית (אופציונלי)
        /// </summary>
        public string SpecificProcName { get; set; }

        /// <summary>
        /// כותרת ספציפית לפרוצדורה (אופציונלי)
        /// </summary>
        public string SpecificAlias { get; set; }
    }
}
