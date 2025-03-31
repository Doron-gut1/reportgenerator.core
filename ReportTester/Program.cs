using System.Data;
using ReportGenerator.Core.Management;
using ReportGenerator.Core.Management.Enums;

namespace ReportTester;

class Program
{
    static async Task Main(string[] args)
    {
        try
        {
            Console.WriteLine("Report Generator Tester");
            Console.WriteLine("======================\n");

            // הגדרת Connection String
            string connectionString = "Server=epr-803-sql\\qa2016;Database=BrnGviaDev;Trusted_Connection=True;TrustServerCertificate=True;";

            string templatePath = @"..\..\..\..\ReportGenerator.Core\Generators\Examples";
            string reportName = "ArnSummaryComplexReport";
            OutputFormat outFormat = OutputFormat.PDF;
            var reportManager = new ReportManager(connectionString, templatePath);
            var parameters = new object[] {
                  "mnt", 275, DbType.Int32,       // חודש מרץ
                "isvkod",0 , DbType.Int32     // קוד ישוב ספציפי
             };

            // הרצת הדוח
            var result = await reportManager.GenerateReport(reportName, outFormat, parameters);

            // שמירת התוצאה לקובץ
            if (outFormat == OutputFormat.PDF)
                File.WriteAllBytes($"{reportName}.pdf", result);
            else
                File.WriteAllBytes($"{reportName}.xlsx", result);

            Console.WriteLine("Report generated successfully!");
        
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

    static async Task TestReport(ReportManager manager, string reportName, object[] parameters)
    {
        try
        {
            // בדיקת PDF
            Console.WriteLine("\nTesting PDF Generation...");
            var pdfResult = await manager.GenerateReport(reportName, OutputFormat.PDF, parameters);
            await File.WriteAllBytesAsync("test_report.pdf", pdfResult);
            Console.WriteLine($"PDF generated successfully. Size: {pdfResult.Length:N0} bytes");

            // בדיקת Excel
            Console.WriteLine("\nTesting Excel Generation...");
            var excelResult = await manager.GenerateReport(reportName, OutputFormat.Excel, parameters);
            await File.WriteAllBytesAsync("test_report.xlsx", excelResult);
            Console.WriteLine($"Excel generated successfully. Size: {excelResult.Length:N0} bytes");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error testing report: {ex.Message}");
            throw;
        }
    }
}