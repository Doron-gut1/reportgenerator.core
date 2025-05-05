using ReportGenerator.Core.Management;
using ReportGenerator.Core.Management.Enums;
using System.Data;

class Program
{
    static void Main(string[] args)
    {
        try
        {
            Console.WriteLine("Report Generator Tester - Using Factory");
            Console.WriteLine("====================================\n");

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

            // נתיב תיקיית פלט
            string outputPath = Path.Combine(AppContext.BaseDirectory, "Output");
            Directory.CreateDirectory(outputPath);
            Console.WriteLine($"Output folder: {outputPath}");

            // שם הדוח להפקה
            string reportName = "TrfbysugtsSummaryReport";
            OutputFormat outFormat = OutputFormat.PDF;

            // פרמטרים לדוח
            var parameters = new object[] {
                "mnt", 275, DbType.Int32
            };

            // יצירת ReportManager באמצעות Factory
            Console.WriteLine("\nיוצר ReportManager באמצעות Factory...");
            var reportManager = ReportManagerFactory.CreateReportManager(
                connectionString,  // מחרוזת חיבור 
                templatePath,      // תיקיית תבניות
                outputPath         // תיקיית פלט
            );

            Console.WriteLine("\nיוצר את הדוח בפורמט אקסל...");
            Console.WriteLine("הדוח יכלול כותרות בעברית ועיצוב משופר");
            
            // הפקת הדוח באופן אסינכרוני
            reportManager.GenerateReportAsync(reportName, outFormat, parameters);
            Console.WriteLine("\nהפקת הדוח החלה. בדוק בתיקיית הפלט.");

            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nError: {ex.Message}");
            var innerEx = ex;
            while (innerEx.InnerException != null)
            {
                innerEx = innerEx.InnerException;
                Console.WriteLine($"Inner Error: {innerEx.Message}");
            }
            Console.WriteLine($"\nStackTrace: {ex.StackTrace}");
            Console.ReadKey();
        }
    }
}