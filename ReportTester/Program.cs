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

            var reportManager = new ReportManager(connectionString, @"X:\team_gvia\Hila\EmailData\MyPdfֹ_YOSEF4.pdf");

            // הגדרת פרמטרים לדוגמה
            var parameters = new object[]
            { "mnt", 277, DbType.Int32
             //,"name", "sds", DbType.String 
            // "DepartmentId", 1, SqlDbType.Int
            };

            await TestReport(reportManager, "arnrepcrntmkomit", parameters);

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