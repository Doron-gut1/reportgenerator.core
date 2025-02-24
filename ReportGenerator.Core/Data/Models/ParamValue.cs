using System.Data;

namespace ReportGenerator.Core.Data.Models
{
    public class ParamValue
    {
        public object Value { get; set; }
        public DbType Type { get; set; }  

        public ParamValue(object value, DbType type)
        {
            Value = value;
            Type = type;
        }
    }
}