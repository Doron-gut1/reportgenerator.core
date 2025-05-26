using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Net.Security;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace RunApiAndSaveReport
{
    internal class Program
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        private static string _apiBaseUrl;
        private static string _defaultOutputFolder;
        private static bool _createFolderIfNotExists;
        private static bool _showDetailedErrors;
        private static bool _logToConsole;
        
        static async Task<int> Main(string[] args)
        {
            try
            {
                // טעינת הגדרות מה-App.config
                LoadConfiguration();
                
                Console.WriteLine("=== Report Generator Client ===");
                Console.WriteLine($"מחובר לשרת: {_apiBaseUrl}");
                Console.WriteLine();
                
                // בדיקת פרמטרי שורת הפקודה
                if (args.Length == 0)
                {
                    ShowUsage();
                    return await RunInteractiveMode();
                }
                else
                {
                    return await RunCommandLineMode(args);
                }
            }
            catch (Exception ex)
            {
                LogError($"שגיאה כללית: {ex.Message}");
                if (_showDetailedErrors)
                {
                    LogError($"פרטים נוספים: {ex}");
                }
                return 1;
            }
            finally
            {
                _httpClient?.Dispose();
            }
        }
        
        private static void LoadConfiguration()
        {
            _apiBaseUrl = ConfigurationManager.AppSettings["ApiBaseUrl"] ?? "http://localhost:5000/api/reports";
            _defaultOutputFolder = ConfigurationManager.AppSettings["DefaultOutputFolder"] ?? "C:\\Reports";
            _createFolderIfNotExists = bool.Parse(ConfigurationManager.AppSettings["CreateFolderIfNotExists"] ?? "true");
            _showDetailedErrors = bool.Parse(ConfigurationManager.AppSettings["ShowDetailedErrors"] ?? "true");
            _logToConsole = bool.Parse(ConfigurationManager.AppSettings["LogToConsole"] ?? "true");
            
            // הגדרת timeout ל-HttpClient
            var timeoutSeconds = int.Parse(ConfigurationManager.AppSettings["ApiTimeout"] ?? "300");
            _httpClient.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
            
            // טיפול ב-HTTPS certificates עבור development
            if (_apiBaseUrl.StartsWith("https://localhost") || _apiBaseUrl.StartsWith("https://127.0.0.1"))
            {
                ServicePointManager.ServerCertificateValidationCallback = 
                    (sender, certificate, chain, sslPolicyErrors) => true;
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
            }
            
            // טיפול ב-HTTPS certificates (לפיתוח בלבד!)
            if (_apiBaseUrl.StartsWith("https"))
            {
                LogInfo("מזוהה שימוש ב-HTTPS - מתעלם מבעיות SSL certificates");
                ServicePointManager.ServerCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true;
            }
            
            LogInfo($"הוענו הגדרות:");
            LogInfo($"  - API URL: {_apiBaseUrl}");
            LogInfo($"  - תיקיית פלט: {_defaultOutputFolder}");
            LogInfo($"  - Timeout: {timeoutSeconds} שניות");
        }
        
        private static void ShowUsage()
        {
            Console.WriteLine("usgate:");
            Console.WriteLine("RunApiAndSaveReport.exe <ReportName> <OutputFormat> <OutputPath> [Parameters]");
            Console.WriteLine();
            Console.WriteLine("examples:");
            Console.WriteLine("  RunApiAndSaveReport.exe ArnSummaryReport PDF C:\\Reports");
            Console.WriteLine("  RunApiAndSaveReport.exe ArnSummaryReport Excel C:\\Reports mnt=3,isvkod=1");
            Console.WriteLine();
            Console.WriteLine("או הרץ ללא פרמטרים למצב אינטראקטיבי");
            Console.WriteLine();
        }
        
        private static async Task<int> RunCommandLineMode(string[] args)
        {
            if (args.Length < 3)
            {
                LogError("מספר פרמטרים לא מספיק. נדרשים לפחות 3 פרמטרים.");
                ShowUsage();
                return 1;
            }
            
            string reportName = args[0];
            string outputFormat = args[1];
            string outputPath = args[2];
            string parameters = args.Length > 3 ? args[3] : null;
            
            return await GenerateAndSaveReport(reportName, outputFormat, outputPath, parameters);
        }
        
        private static async Task<int> RunInteractiveMode()
        {
            Console.WriteLine("=== מצב אינטראקטיבי ===");
            
            try
            {
                // קבלת שם הדוח
                Console.Write("Report name: ");
                string reportName = Console.ReadLine()?.Trim();
                
                if (string.IsNullOrEmpty(reportName))
                {
                    LogError("שם דוח לא יכול להיות ריק.");
                    return 1;
                }
                
                // קבלת פורמט הפלט
                Console.Write("FORMAT (PDF/Excel): ");
                string outputFormat = Console.ReadLine()?.Trim();
                
                if (string.IsNullOrEmpty(outputFormat))
                {
                    outputFormat = "PDF";
                    LogInfo("נבחר פורמט ברירת מחדל: PDF");
                }
                
                // קבלת נתיב שמירה
                Console.Write($"SAVE TO ? {_defaultOutputFolder}): ");
                string outputPath = Console.ReadLine()?.Trim();
                
                if (string.IsNullOrEmpty(outputPath))
                {
                    outputPath = _defaultOutputFolder;
                }
                
                // קבלת פרמטרים (אופציונלי)
                Console.Write("PARAMETERS: key1=value1,key2=value2): ");
                string parameters = Console.ReadLine()?.Trim();
                
                return await GenerateAndSaveReport(reportName, outputFormat, outputPath, parameters);
            }
            catch (Exception ex)
            {
                LogError($"שגיאה במצב אינטראקטיבי: {ex.Message}");
                return 1;
            }
        }
        
        private static async Task<int> GenerateAndSaveReport(string reportName, string outputFormat, string outputPath, string parameters)
        {
            try
            {
                LogInfo($"מתחיל הפקת דוח: {reportName}");
                LogInfo($"פורמט: {outputFormat}");
                LogInfo($"נתיב שמירה: {outputPath}");
                LogInfo($"פרמטרים: {parameters ?? "(WITOUT)"}");
                
                // הכנת בקשה ל-API
                var request = new GenerateReportRequest
                {
                    ReportName = reportName,
                    OutputFormat = outputFormat,
                    Parameters = ParseParameters(parameters)
                };
                
                // לוג של הפרמטרים המעובדים
                LogInfo($"פרמטרים מעובדים: {request.Parameters?.Count ?? 0} פרמטרים");
                foreach (var param in request.Parameters ?? new Dictionary<string, ReportParameterModel>())
                {
                    LogInfo($"  {param.Key} = {param.Value.Value} ({param.Value.Type})");
                }
                
                // שליחת הבקשה
                LogInfo("שולח בקשה ל-API...");
                var reportBytes = await CallApiForReportBytes(request);
                
                if (reportBytes == null)
                {
                    LogError("הפקת הדוח נכשלה - לא התקבלו נתונים מהשרת.");
                    return 1;
                }
                
                // שמירת הקובץ
                LogInfo($"שומר קובץ בגודל {reportBytes.Length:N0} bytes...");
                string savedFilePath = await SaveReportFile(reportBytes, reportName, outputFormat, outputPath);
                
                LogInfo($"הדוח נשמר בהצלחה: {savedFilePath}");
                return 0;
            }
            catch (Exception ex)
            {
                LogError($"שגיאה בהפקת הדוח: {ex.Message}");
                if (_showDetailedErrors)
                {
                    LogError($"פרטים נוספים: {ex}");
                }
                return 1;
            }
        }
        
        private static async Task<byte[]> CallApiForReportBytes(GenerateReportRequest request)
        {
            try
            {
                // הדפסת הבקשה לדיבוג
                LogInfo($"מכין בקשה עבור דוח: {request.ReportName}");
                LogInfo($"פורמט: {request.OutputFormat}");
                if (request.Parameters != null && request.Parameters.Count > 0)
                {
                    LogInfo("פרמטרים:");
                    foreach (var param in request.Parameters)
                    {
                        LogInfo($"  - {param.Key}: {param.Value.Value} ({param.Value.Type})");
                    }
                }
                else
                {
                    LogInfo("ללא פרמטרים");
                }
                
                string jsonContent = JsonConvert.SerializeObject(request, Formatting.Indented);
                LogInfo($"JSON שנשלח: {jsonContent}");
                
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                
                string apiUrl = $"{_apiBaseUrl}/generate-bytes";
                LogInfo($"קורא ל-API: {apiUrl}");
                
                var response = await _httpClient.PostAsync(apiUrl, content);
                
                LogInfo($"תגובה מהשרת: {response.StatusCode} ({(int)response.StatusCode})");
                LogInfo($"Content-Type: {response.Content.Headers.ContentType}");
                LogInfo($"Content-Length: {response.Content.Headers.ContentLength}");
                
                if (response.IsSuccessStatusCode)
                {
                    LogInfo("הבקשה הצליחה, מקבל נתונים...");
                    var resultBytes = await response.Content.ReadAsByteArrayAsync();
                    LogInfo($"התקבלו {resultBytes.Length:N0} bytes");
                    return resultBytes;
                }
                else
                {
                    LogError($"שגיאה בAPI: {response.StatusCode} - {response.ReasonPhrase}");
                    
                    // ניסיון לקרוא הודעת שגיאה מפורטת
                    try
                    {
                        string errorContent = await response.Content.ReadAsStringAsync();
                        if (!string.IsNullOrEmpty(errorContent))
                        {
                            LogError($"פרטי השגיאה: {errorContent}");
                        }
                    }
                    catch { /* התעלמות משגיאה בקריאת הודעת השגיאה */ }
                    
                    return null;
                }
            }
            catch (HttpRequestException ex)
            {
                LogError($"שגיאת רשת: {ex.Message}");
                if (ex.InnerException != null)
                {
                    LogError($"שגיאה פנימית: {ex.InnerException.Message}");
                }
                return null;
            }
            catch (TaskCanceledException ex)
            {
                LogError($"הבקשה הופסקה בעקבות timeout: {ex.Message}");
                return null;
            }
            catch (Exception ex)
            {
                LogError($"שגיאה כללית בקריאה לAPI: {ex.Message}");
                if (_showDetailedErrors)
                {
                    LogError($"פרטים: {ex}");
                }
                return null;
            }
        }
        
        private static async Task<string> SaveReportFile(byte[] reportBytes, string reportName, string outputFormat, string outputPath)
        {
            // יצירת תיקייה אם לא קיימת
            if (_createFolderIfNotExists && !Directory.Exists(outputPath))
            {
                LogInfo($"יוצר תיקייה: {outputPath}");
                Directory.CreateDirectory(outputPath);
            }
            
            // קביעת סיומת הקובץ
            string extension = outputFormat.ToLower() == "pdf" ? ".pdf" : ".xlsx";
            
            // יצירת שם קובץ ייחודי
            string fileName = $"{reportName}_{DateTime.Now:yyyyMMdd_HHmmss}{extension}";
            string fullPath = Path.Combine(outputPath, fileName);
            
            // וידוי שהקובץ לא קיים (למקרה של הפעלות מהירות)
            int counter = 1;
            while (File.Exists(fullPath))
            {
                fileName = $"{reportName}_{DateTime.Now:yyyyMMdd_HHmmss}_{counter:00}{extension}";
                fullPath = Path.Combine(outputPath, fileName);
                counter++;
            }
            
            // שמירת הקובץ
            File.WriteAllBytes(fullPath, reportBytes);
            
            // וידוא שהקובץ נשמר בהצלחה
            if (!File.Exists(fullPath))
            {
                throw new IOException($"הקובץ לא נשמר בהצלחה: {fullPath}");
            }
            
            return fullPath;
        }
        
        private static Dictionary<string, ReportParameterModel> ParseParameters(string parameters)
        {
            var result = new Dictionary<string, ReportParameterModel>();
            
            if (string.IsNullOrEmpty(parameters))
                return result;
            
            try
            {
                var pairs = parameters.Split(',');
                foreach (var pair in pairs)
                {
                    var keyValue = pair.Split('=');
                    if (keyValue.Length == 2)
                    {
                        string key = keyValue[0].Trim();
                        string value = keyValue[1].Trim();
                        
                        // ניסיון לזהות סוג הנתונים
                        string dataType = "String";
                        object convertedValue = value;
                        
                        if (int.TryParse(value, out int intValue))
                        {
                            dataType = "Int32";
                            convertedValue = intValue;
                        }
                        else if (DateTime.TryParse(value, out DateTime dateValue))
                        {
                            dataType = "DateTime";
                            convertedValue = dateValue;
                        }
                        else if (bool.TryParse(value, out bool boolValue))
                        {
                            dataType = "Boolean";
                            convertedValue = boolValue;
                        }
                        
                        result[key] = new ReportParameterModel
                        {
                            Value = convertedValue,
                            Type = dataType
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                LogError($"שגיאה בפענוח פרמטרים: {ex.Message}");
            }
            
            return result;
        }
        
        private static void LogInfo(string message)
        {
            if (_logToConsole)
            {
                Console.WriteLine($"[INFO] {DateTime.Now:HH:mm:ss} - {message}");
            }
        }
        
        private static void LogError(string message)
        {
            if (_logToConsole)
            {
                var originalColor = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[ERROR] {DateTime.Now:HH:mm:ss} - {message}");
                Console.ForegroundColor = originalColor;
            }
        }
    }
    
    // מודלים תואמים ל-API
    public class GenerateReportRequest
    {
        public string ReportName { get; set; }
        public string DepartmentId { get; set; }
        public string OutputFormat { get; set; }
        public Dictionary<string, ReportParameterModel> Parameters { get; set; }
    }
    
    public class ReportParameterModel
    {
        public object Value { get; set; }
        public string Type { get; set; }
    }
}
