using System.Threading.Tasks;

namespace ReportGenerator.Core.Services
{
    /// <summary>
    /// ממשק לניהול תבניות HTML
    /// </summary>
    public interface ITemplateManager
    {
        /// <summary>
        /// מחזיר את נתיב התיקייה בה מאוחסנות התבניות
        /// </summary>
        string TemplatesFolder { get; }
        
        /// <summary>
        /// בודק אם תבנית קיימת
        /// </summary>
        /// <param name="templateName">שם התבנית (ללא סיומת)</param>
        /// <returns>האם התבנית קיימת</returns>
        bool TemplateExists(string templateName);
        
        /// <summary>
        /// מקבל את תוכן התבנית
        /// </summary>
        /// <param name="templateName">שם התבנית (ללא סיומת)</param>
        /// <returns>תוכן התבנית כמחרוזת</returns>
        Task<string> GetTemplateAsync(string templateName);
        
        /// <summary>
        /// מקבל את הנתיב המלא לתבנית
        /// </summary>
        /// <param name="templateName">שם התבנית (ללא סיומת)</param>
        /// <returns>נתיב מלא לקובץ התבנית</returns>
        string GetTemplateFullPath(string templateName);
        
        /// <summary>
        /// שומר תבנית חדשה או מעדכן קיימת
        /// </summary>
        /// <param name="templateName">שם התבנית (ללא סיומת)</param>
        /// <param name="templateContent">תוכן התבנית</param>
        /// <returns>האם השמירה הצליחה</returns>
        Task<bool> SaveTemplateAsync(string templateName, string templateContent);
    }
}