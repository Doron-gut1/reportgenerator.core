using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ReportGenerator.Core.Configuration;
using ReportGenerator.Core.Data;
using ReportGenerator.Core.Errors;
using ReportGenerator.Core.Generators;
using ReportGenerator.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace ReportGenerator.Core.Management
{
    public static class ReportManagerFactory
    {
        private static IServiceProvider? _serviceProvider;

        public static ReportManager CreateReportManager(string connectionString, string templatesFolder, string outputFolder)
        {
            if (_serviceProvider == null)
            {
                var services = new ServiceCollection();

                // הגדרות
                var reportSettings = new ReportSettings
                {
                    ConnectionString = connectionString,
                    TemplatesFolder = templatesFolder,
                    OutputFolder = outputFolder,
                    LogsFolder = Path.Combine(AppContext.BaseDirectory, "Logs")
                };

                // רישום ההגדרות וכל התלויות
                services.Configure<ReportSettings>(options => {
                    options.ConnectionString = reportSettings.ConnectionString;
                    options.TemplatesFolder = reportSettings.TemplatesFolder;
                    options.OutputFolder = reportSettings.OutputFolder;
                    options.LogsFolder = reportSettings.LogsFolder;
                });

                // רישום כל התלויות
                services.AddTransient<IDataAccess, DataAccess>();
                services.AddTransient<ITemplateManager, HtmlTemplateManager>();
                services.AddTransient<ITemplateProcessor, HtmlTemplateProcessor>();
                services.AddTransient<IPdfGenerator, HtmlBasedPdfGenerator>();
                services.AddTransient<IExcelGenerator, ExcelGenerator>();
                services.AddTransient<IErrorManager, ErrorManager>();
                services.AddTransient<IErrorLogger, DbErrorLogger>();
                services.AddTransient<IHtmlToPdfConverter, PuppeteerHtmlToPdfConverter>();
                services.AddTransient<IReportGenerator, ReportManager>();

                _serviceProvider = services.BuildServiceProvider();
            }

            return (ReportManager)_serviceProvider.GetRequiredService<IReportGenerator>();
        }
    }
}