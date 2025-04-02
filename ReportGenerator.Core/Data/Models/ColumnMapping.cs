namespace ReportGenerator.Core.Data.Models
{
    /// <summary>
    /// מודל המייצג מיפוי בין שמות עמודות באנגלית לעברית
    /// </summary>
    public class ColumnMapping
    {

        /// שם הטבלה הלוגי

        public string TableName { get; set; }


        /// שם העמודה באנגלית

        public string ColumnName { get; set; }


        /// הכותרת בעברית (גנרי)

        public string HebrewAlias { get; set; }


        /// שם פרוצדורה ספציפית (אופציונלי)

        public string SpecificProcName { get; set; }


        /// כותרת ספציפית לפרוצדורה (אופציונלי)

        public string SpecificAlias { get; set; }
        

        /// האם זהו שדה קבוע מטבלה (עם "_") או שדה מחושב

        public bool IsTableField => TableName != SpecificProcName;
    }
}