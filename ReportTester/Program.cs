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
            string reportName = "TrfbysugtsSummaryReport";
            OutputFormat outFormat = OutputFormat.PDF;
            var reportManager = new ReportManager(connectionString, templatePath);

            var parameters = new object[] {
                "mnt", 275, DbType.Int32
                //",isvme", null, DbType.Int32,
                //"isvad", null, DbType.Int32,
                //"sughskod", null, DbType.Int32,
                //"midgam", null, DbType.Boolean,
                //"bysughs", null, DbType.Boolean,
                //"hanhkkrts", null, DbType.Boolean
            };
           // var parameters = new object[] {
              //    "mnt", 277, DbType.Int32//,       // חודש מרץ
               // "isvkod",null , DbType.Int32 ,    // קוד ישוב ספציפי
              //  "sugtslist","706", DbType.String
           //  };
            //var parameters = new object[] {
            //    "byyr", 0, DbType.Int32,
            //    "thisyr", 2021, DbType.Int32,
            //    "frstdt", DateTime.Parse("2021-01-01"), DbType.DateTime,
            //    "lastdate", DateTime.Parse("2021-02-28"), DbType.DateTime,
            //    "sugts", 1010, DbType.Int32,
            //    "sugtsname", null, DbType.String,  // פרמטר נוסף לתצוגה בדוח
            //    "hanmas", null, DbType.Int32,
            //    "isvkod", null, DbType.Int32
            //};
            reportManager.GenerateReportAsync(reportName, outFormat, parameters);
        
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