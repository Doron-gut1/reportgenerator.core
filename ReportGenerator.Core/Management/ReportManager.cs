using System;
using System.Threading.Tasks;
using System.Data;
using System.Collections.Generic;
using ReportGenerator.Core.Data;
using ReportGenerator.Core.Data.Models;
using ReportGenerator.Core.Generators;
using ReportGenerator.Core.Management.Enums;

namespace ReportGenerator.Core.Management
{
    public class ReportManager
    {
        private readonly DataAccess _dataAccess;
        private readonly PdfGenerator _pdfGenerator;
        private readonly ExcelGenerator _excelGenerator;

        public ReportManager(string connectionString, string pdfTemplatePath)
        {
            _dataAccess = new DataAccess(connectionString);
            _pdfGenerator = new PdfGenerator(pdfTemplatePath);
            _excelGenerator = new ExcelGenerator();
        }

        public async Task<byte[]> GenerateReport(string reportName, OutputFormat format, params object[] parameters)
        {
            try
            {
                // המרת פרמטרים למילון
                var parsedParams = ParseParameters(parameters);

                // קבלת שמות הפרוצדורות
                string storedProcNames = await _dataAccess.GetStoredProcName(reportName);

                // הרצת כל הפרוצדורות ומיזוג התוצאות
                var data = await _dataAccess.ExecuteMultipleStoredProcedures(storedProcNames, parsedParams);

                // יצירת הדוח בפורמט המבוקש
                return format == OutputFormat.PDF
                    ? _pdfGenerator.Generate(data)
                    : _excelGenerator.Generate(data);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error generating report {reportName}: {ex.Message}", ex);
            }
        }

        private Dictionary<string, ParamValue> ParseParameters(object[] paramArray)
        {
            var result = new Dictionary<string, ParamValue>(StringComparer.OrdinalIgnoreCase);
            try
            {
                for (int i = 0; i < paramArray.Length; i += 3)
                {
                    if (i + 2 >= paramArray.Length)
                        throw new ArgumentException("Parameter array is not in the correct format");

                    string paramName = paramArray[i]?.ToString() ??
                        throw new ArgumentException($"Parameter name at position {i} is null");

                    object paramValue = paramArray[i + 1];
                    DbType paramType = (DbType)paramArray[i + 2];

                    result.Add(paramName, new ParamValue(paramValue, paramType));
                }
                return result;
            }
            catch (Exception ex)
            {
                throw new ArgumentException("Error parsing parameters array", ex);
            }
        }
    }
}