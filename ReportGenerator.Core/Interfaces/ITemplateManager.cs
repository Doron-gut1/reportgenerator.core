using System.Threading.Tasks;

namespace ReportGenerator.Core.Interfaces
{
    /// <summary>
    /// ממשק לניהול תבניות HTML
    /// </summary>
    public interface ITemplateManager
    {
        /// <summary>
        /// בודק אם תבנית קיימת
        /// </summary>
        /// <param name="templateName">שם התבנית</param>
        /// <param name="departmentId">מזהה המחלקה (אופציונלי)</param>
        /// <returns>האם התבנית קיימת</returns>
        bool TemplateExists(string templateName, string? departmentId = null);

        /// <summary>
        /// מקבל תוכן של תבנית HTML
        /// </summary>
        /// <param name="templateName">שם התבנית</param>
        /// <param name="departmentId">מזהה המחלקה (אופציונלי)</param>
        /// <returns>תוכן התבנית</returns>
        Task<string> GetTemplateAsync(string templateName, string? departmentId = null);
    }
}
