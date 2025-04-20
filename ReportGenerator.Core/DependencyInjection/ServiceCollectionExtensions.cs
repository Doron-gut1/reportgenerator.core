using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ReportGenerator.Core.Configuration;
using ReportGenerator.Core.Data;
using ReportGenerator.Core.Errors;
using ReportGenerator.Core.Generators;
using ReportGenerator.Core.Interfaces;
using ReportGenerator.Core.Management;

namespace ReportGenerator.Core.DependencyInjection
{
    /// <summary>
    /// הרחבות למערכת הזרקת התלויות
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// הוספת שירותי מערכת הדוחות
        /// </summary>
        /// <param name="services">אוסף השירותים</param>
        /// <param name="configuration">הקונפיגורציה</param>
        /// <returns>אוסף השירותים עם התוספות</returns>
        public static IServiceCollection AddReportGenerator(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // קריאת הגדרות מהקונפיגורציה
            services.Configure<ReportSettings>(options => 
                configuration.GetSection("ReportSettings").Bind(options));

            // רישום שירותים
            services.AddTransient<IDataAccess, DataAccess>();
            services.AddTransient<ITemplateManager, HtmlTemplateManager>();
            services.AddTransient<ITemplateProcessor, HtmlTemplateProcessor>();
            services.AddTransient<IHtmlToPdfConverter, PuppeteerHtmlToPdfConverter>();
            services.AddTransient<IPdfGenerator, HtmlBasedPdfGenerator>();
            services.AddTransient<IExcelGenerator, ExcelGenerator>();
            services.AddTransient<IReportGenerator, ReportManager>();
            services.AddSingleton<IErrorLogger, DbErrorLogger>();
            services.AddSingleton<IErrorManager, ErrorManager>();

            return services;
        }

        /// <summary>
        /// הוספת שירותי מערכת הדוחות עם הגדרות מותאמות אישית
        /// </summary>
        /// <param name="services">אוסף השירותים</param>
        /// <param name="settings">הגדרות מערכת הדוחות</param>
        /// <returns>אוסף השירותים עם התוספות</returns>
        public static IServiceCollection AddReportGenerator(
            this IServiceCollection services,
            ReportSettings settings)
        {
            // רישום ההגדרות
            services.Configure<ReportSettings>(options =>
            {
                options.ConnectionString = settings.ConnectionString;
                options.TemplatesFolder = settings.TemplatesFolder;
                options.OutputFolder = settings.OutputFolder;
                //options.TempFolder = settings.TempFolder;
                options.LogsFolder = settings.LogsFolder;
                options.ChromePath = settings.ChromePath;
                options.AutoDownloadChrome = settings.AutoDownloadChrome;
            });

            // רישום שירותים
            services.AddTransient<IDataAccess, DataAccess>();
            services.AddTransient<ITemplateManager, HtmlTemplateManager>();
            services.AddTransient<ITemplateProcessor, HtmlTemplateProcessor>();
            services.AddTransient<IHtmlToPdfConverter, PuppeteerHtmlToPdfConverter>();
            services.AddTransient<IPdfGenerator, HtmlBasedPdfGenerator>();
            services.AddTransient<IExcelGenerator, ExcelGenerator>();
            services.AddTransient<IReportGenerator, ReportManager>();
            services.AddSingleton<IErrorLogger, DbErrorLogger>();
            services.AddSingleton<IErrorManager, ErrorManager>();

            return services;
        }
    }
}
