using Microsoft.Extensions.DependencyInjection;
using ReportGenerator.Core.Configuration;
using ReportGenerator.Core.Data;
using ReportGenerator.Core.Errors;
using ReportGenerator.Core.Generators;
using ReportGenerator.Core.Interfaces;
using ReportGenerator.Core.Management.Enums;
using ReportGenerator.Core.Management;
using System.Data;

class Program
{
    static async Task Main(string[] args)
    {
        try
        {
            Console.WriteLine("Report Generator Tester");
            Console.WriteLine("======================\n");

            var services = new ServiceCollection();

            // הגדרת Connection String
            string connectionString = "Server=epr-803-sql\\qa2016;Database=BrnGviaDev;Trusted_Connection=True;TrustServerCertificate=True;";

            // יצירת נתיב מוחלט לתיקיית התבניות
            string templatePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\ReportGenerator.Core\Generators\Examples"));
            Console.WriteLine($"Template path: {templatePath}");

            // וידוא שהתיקייה קיימת
            if (!Directory.Exists(templatePath))
            {
                Console.WriteLine($"Warning: Template directory does not exist: {templatePath}");
                // ניתן ליצור את התיקייה או להשתמש בנתיב אחר
            }

            string reportName = "TrfbysugtsSummaryReport";

            var reportSettings = new ReportSettings
            {
                ConnectionString = connectionString,
                TemplatesFolder = templatePath,
                OutputFolder = Path.Combine(AppContext.BaseDirectory, "Output"),
                LogsFolder = Path.Combine(AppContext.BaseDirectory, "Logs")
            };

            // וידוא שתיקיות קיימות
            Directory.CreateDirectory(reportSettings.OutputFolder);
            //Directory.CreateDirectory(reportSettings.TempFolder);
            Directory.CreateDirectory(reportSettings.LogsFolder);

            Console.WriteLine($"Output folder: {reportSettings.OutputFolder}");

            OutputFormat outFormat = OutputFormat.Excel;

            // רישום השירותים באופן מפורט
            services.Configure<ReportSettings>(options =>
            {
                options.ConnectionString = reportSettings.ConnectionString;
                options.TemplatesFolder = reportSettings.TemplatesFolder;
                options.OutputFolder = reportSettings.OutputFolder;
                options.LogsFolder = reportSettings.LogsFolder;
            });

            // רישום שירותים ספציפיים
            services.AddTransient<IDataAccess, DataAccess>();
            services.AddTransient<ITemplateManager, HtmlTemplateManager>();
            services.AddTransient<ITemplateProcessor, HtmlTemplateProcessor>();
            services.AddTransient<IHtmlToPdfConverter, PuppeteerHtmlToPdfConverter>();
            services.AddTransient<IPdfGenerator, HtmlBasedPdfGenerator>();
            services.AddTransient<IExcelGenerator>(sp => {
                var dataAccess = sp.GetRequiredService<IDataAccess>();
                var columnMappings = dataAccess.GetDefaultColumnMappings().GetAwaiter().GetResult();
                return new ExcelGenerator(columnMappings);
            });
            services.AddTransient<IReportGenerator, ReportManager>();
            services.AddSingleton<IErrorLogger, DbErrorLogger>();
            services.AddSingleton<IErrorManager, ErrorManager>();

            // בניית הקונטיינר
            var serviceProvider = services.BuildServiceProvider();

            // קבלת מופע של ReportGenerator
            var reportGenerator = serviceProvider.GetRequiredService<IReportGenerator>();

            // קריאה לפונקציה להפקת דוח
            var parameters = new object[] {
                "mnt", 275, DbType.Int32
            };

            Console.WriteLine("\nיוצר מחדש את הדוח בפורמט אקסל...");
            Console.WriteLine("הדוח יכלול כותרות בעברית ועיצוב משופר");
            
            // הפקת הדוח עם לוג מידע נוסף
            reportGenerator.GenerateReportAsync(reportName, outFormat, parameters);
            Console.WriteLine("\nהפקת הדוח החלה. בדוק בתיקיית הפלט.");

            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nError: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Inner Error: {ex.InnerException.Message}");
            }
            Console.WriteLine($"\nStackTrace: {ex.StackTrace}");
            Console.ReadKey();
        }
    }
}