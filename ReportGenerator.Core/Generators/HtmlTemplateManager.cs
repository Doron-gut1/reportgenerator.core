using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReportGenerator.Core.Generators
{
    /// <summary>
    /// מנהל תבניות HTML - אחראי על טעינה, שמירה וניהול של קבצי תבניות HTML
    /// </summary>
    public class HtmlTemplateManager
    {
        private readonly string _templatesFolder;

        /// <summary>
        /// יוצר מופע חדש של מנהל התבניות
        /// </summary>
        /// <param name="templatesFolder">נתיב לתיקיית התבניות</param>
        public HtmlTemplateManager(string templatesFolder)
        {
            if (string.IsNullOrEmpty(templatesFolder))
                throw new ArgumentNullException(nameof(templatesFolder));

            _templatesFolder = templatesFolder;

            // וידוא שהתיקייה קיימת
            if (!Directory.Exists(_templatesFolder))
                Directory.CreateDirectory(_templatesFolder);
        }

        /// <summary>
        /// בודק אם תבנית קיימת
        /// </summary>
        /// <param name="templateName">שם התבנית (ללא סיומת)</param>
        /// <returns>האם התבנית קיימת</returns>
        public bool TemplateExists(string templateName)
        {
            string fullPath = GetTemplatePath(templateName);
            return File.Exists(fullPath);
        }

        /// <summary>
        /// מקבל רשימה של כל התבניות הזמינות
        /// </summary>
        /// <returns>רשימת שמות תבניות (ללא סיומת)</returns>
        public IEnumerable<string> GetAvailableTemplates()
        {
            return Directory.GetFiles(_templatesFolder, "*.html")
                .Select(file => Path.GetFileNameWithoutExtension(file));
        }

        /// <summary>
        /// טוען תבנית HTML מהדיסק
        /// </summary>
        /// <param name="templateName">שם התבנית (ללא סיומת)</param>
        /// <returns>תוכן התבנית כמחרוזת</returns>
        public async Task<string> GetTemplateAsync(string templateName)
        {
            string fullPath = GetTemplatePath(templateName);

            if (!File.Exists(fullPath))
                throw new FileNotFoundException($"Template '{templateName}' not found at {fullPath}");

            return await File.ReadAllTextAsync(fullPath);
        }

        /// <summary>
        /// שומר תבנית HTML לדיסק
        /// </summary>
        /// <param name="templateName">שם התבנית (ללא סיומת)</param>
        /// <param name="content">תוכן התבנית</param>
        public async Task SaveTemplateAsync(string templateName, string content)
        {
            string fullPath = GetTemplatePath(templateName);
            await File.WriteAllTextAsync(fullPath, content);
        }

        /// <summary>
        /// מקבל את הנתיב המלא לקובץ התבנית
        /// </summary>
        /// <param name="templateName">שם התבנית (ללא סיומת)</param>
        /// <returns>נתיב מלא לקובץ</returns>
        private string GetTemplatePath(string templateName)
        {
            // ניקוי שם הקובץ משמות תווים אסורים
            string safeFileName = string.Join("_", templateName.Split(Path.GetInvalidFileNameChars()));
            return Path.Combine(_templatesFolder, $"{safeFileName}.html");
        }
    }
}
