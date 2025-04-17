namespace ReportGenerator.Core.Data.Models
{
    /// <summary>
    /// מידע על פרמטר בפרוצדורה
    /// </summary>
    public class ParameterInfo
    {
        /// <summary>
        /// שם הפרמטר
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// סוג הנתונים
        /// </summary>
        public string DataType { get; set; } = string.Empty;

        /// <summary>
        /// ערך ברירת מחדל (אם קיים)
        /// </summary>
        public string? DefaultValue { get; set; }

        /// <summary>
        /// האם הפרמטר יכול לקבל ערך Null
        /// </summary>
        public bool IsNullable { get; set; }

        public bool IsOptional { get; set; }
    }
}
