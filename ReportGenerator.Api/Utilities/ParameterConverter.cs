using System.Data;
using ReportGenerator.Api.Models;

namespace ReportGenerator.Api.Utilities
{
    /// <summary>
    /// ממשק להמרת פרמטרים
    /// </summary>
    public interface IParameterConverter
    {
        /// <summary>
        /// המרת פרמטרים מהבקשה למערך אובייקטים
        /// </summary>
        object[] ConvertRequestParameters(Dictionary<string, ReportParameterModel>? requestParams);
        
        /// <summary>
        /// המרת מחרוזת פורמט לסוג האמיתי
        /// </summary>
        ReportGenerator.Core.Management.Enums.OutputFormat ConvertOutputFormat(string format);
    }

    /// <summary>
    /// ממיר פרמטרים המתרגם בין בקשת API לפרמטרים של הספרייה
    /// </summary>
    public class ParameterConverter : IParameterConverter
    {
        private readonly ILogger<ParameterConverter> _logger;

        public ParameterConverter(ILogger<ParameterConverter> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// המרת פרמטרים מהבקשה למערך אובייקטים עבור הספרייה הקיימת
        /// </summary>
        public object[] ConvertRequestParameters(Dictionary<string, ReportParameterModel>? requestParams)
        {
            var parameters = new List<object>();

            if (requestParams == null || !requestParams.Any())
            {
                _logger.LogWarning("לא התקבלו פרמטרים בבקשה");
                return parameters.ToArray();
            }

            foreach (var param in requestParams)
            {
                // הוספת שם הפרמטר
                parameters.Add(param.Key);

                // הוספת ערך הפרמטר (לאחר המרה אם צריך)
                parameters.Add(ConvertParameterValue(param.Value));

                // הוספת סוג הפרמטר
                parameters.Add(ConvertParameterType(param.Value.Type));
            }

            return parameters.ToArray();
        }

        /// <summary>
        /// המרת ערך הפרמטר בהתאם לסוג
        /// </summary>
        private object ConvertParameterValue(ReportParameterModel parameter)
        {
            if (parameter.Value == null)
                return DBNull.Value;

            try
            {
                return parameter.Type.ToLower() switch
                {
                    "int32" or "int" => Convert.ToInt32(parameter.Value),
                    "int64" or "long" => Convert.ToInt64(parameter.Value),
                    "decimal" => Convert.ToDecimal(parameter.Value),
                    "double" => Convert.ToDouble(parameter.Value),
                    "date" or "datetime" => ConvertToDateTime(parameter.Value),
                    "boolean" or "bool" => Convert.ToBoolean(parameter.Value),
                    "string" => parameter.Value.ToString() ?? string.Empty,
                    _ => parameter.Value
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "שגיאה בהמרת ערך פרמטר מסוג {Type}", parameter.Type);
                return parameter.Value;
            }
        }

        /// <summary>
        /// המרת ערך לתאריך
        /// </summary>
        private static DateTime ConvertToDateTime(object value)
        {
            if (value is DateTime dateTime)
                return dateTime;

            if (value is DateTimeOffset dateTimeOffset)
                return dateTimeOffset.DateTime;

            var stringValue = value.ToString();
            if (DateTime.TryParse(stringValue, out var parsedDate))
                return parsedDate;

            throw new ArgumentException($"לא ניתן להמיר את הערך '{value}' לתאריך");
        }

        /// <summary>
        /// המרת מחרוזת סוג לDbType
        /// </summary>
        private static DbType ConvertParameterType(string typeName)
        {
            return typeName.ToLower() switch
            {
                "int32" or "int" => DbType.Int32,
                "int64" or "long" => DbType.Int64,
                "decimal" => DbType.Decimal,
                "double" => DbType.Double,
                "date" => DbType.Date,
                "datetime" => DbType.DateTime,
                "boolean" or "bool" => DbType.Boolean,
                "string" or "nvarchar" => DbType.String,
                _ => DbType.String
            };
        }

        /// <summary>
        /// המרת מחרוזת פורמט לסוג האמיתי
        /// </summary>
        public ReportGenerator.Core.Management.Enums.OutputFormat ConvertOutputFormat(string format)
        {
            if (string.IsNullOrEmpty(format))
                return ReportGenerator.Core.Management.Enums.OutputFormat.PDF;

            return format.ToLower() switch
            {
                "excel" or "xlsx" => ReportGenerator.Core.Management.Enums.OutputFormat.Excel,
                _ => ReportGenerator.Core.Management.Enums.OutputFormat.PDF
            };
        }
    }
}
