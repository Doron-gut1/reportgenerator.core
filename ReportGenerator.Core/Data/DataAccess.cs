using Dapper;
using Microsoft.Data.SqlClient;
using ReportGenerator.Core.Data.Models;
using System.Data;

namespace ReportGenerator.Core.Data
{
    public class DataAccess
    {
        private readonly string _connectionString;

        public DataAccess(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        }

        public async Task<string> GetStoredProcName(string reportName)
        {
            using var connection = new SqlConnection(_connectionString);
            var result = await connection.QuerySingleOrDefaultAsync<string>(
                "SELECT StoredProcName FROM ReportsGenerator  WHERE ReportName = @ReportName",
                new { ReportName = reportName });

            return result ?? throw new Exception($"Report Name {reportName} not found");
        }

        private async Task<IEnumerable<ParameterInfo>> GetProcedureParameters(string procName)
        {
            using var connection = new SqlConnection(_connectionString);
            return await connection.QueryAsync<ParameterInfo>(
                @"SELECT 
            p.name as Name,
            t.name as DataType,
            CASE 
                WHEN p.has_default_value = 1 THEN 'YES'  -- אם יש ערך ברירת מחדל
                WHEN definition LIKE '%' + p.name + '%=' THEN 'YES'  -- בדיקה בהגדרת הפרוצדורה
                ELSE 'NO' 
            END as IsNullable,
            p.default_value as DefaultValue,
            p.parameter_id as ParameterOrder
        FROM sys.parameters p
        INNER JOIN sys.types t ON p.system_type_id = t.system_type_id
        INNER JOIN sys.procedures sp ON p.object_id = sp.object_id
        LEFT JOIN sys.sql_modules m ON sp.object_id = m.object_id
        WHERE sp.name = @ProcName
        ORDER BY p.parameter_id",
                new { ProcName = procName });
        }

        public async Task<DataTable> ExecuteMultipleStoredProcedures(string storedProcNames, Dictionary<string, ParamValue> parameters)
        {
            var procNames = storedProcNames.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            var mainTable = new DataTable();

            foreach (var procName in procNames)
            {
                try
                {
                    // פשוט מריצים את הפרוצדורה עם הפרמטרים שיש
                    var procData = await ExecuteStoredProcedure(procName.Trim(), parameters);
                    MergeDataTables(mainTable, procData);
                }
                catch (Exception ex)
                {
                    throw new Exception($"Error executing stored procedure {procName}: {ex.Message}", ex);
                }
            }

            return mainTable;
        }

        private async Task<DataTable> ExecuteStoredProcedure(string spName, Dictionary<string, ParamValue> parameters)
        {
            using var connection = new SqlConnection(_connectionString);

            var dynamicParams = new DynamicParameters();
            foreach (var param in parameters)
            {
                dynamicParams.Add(
                    param.Key,
                    param.Value.Value ?? DBNull.Value,
                    param.Value.Type);
            }

            var result = await connection.QueryAsync(
                spName,
                dynamicParams,
                commandType: CommandType.StoredProcedure);

            return ToDataTable(result);
        }

        private void MergeDataTables(DataTable mainTable, DataTable newData)
        {
            // אם הטבלה הראשית ריקה, נוסיף את כל העמודות
            if (mainTable.Columns.Count == 0)
            {
                foreach (DataColumn col in newData.Columns)
                {
                    mainTable.Columns.Add(col.ColumnName, col.DataType);
                }
            }
            // אם יש עמודות חדשות, נוסיף אותן
            else
            {
                foreach (DataColumn col in newData.Columns)
                {
                    string columnName = col.ColumnName;
                    if (!mainTable.Columns.Contains(columnName))
                    {
                        mainTable.Columns.Add(columnName, col.DataType);
                    }
                }
            }

            // העתקת כל השורות מהטבלה החדשה
            foreach (DataRow newRow in newData.Rows)
            {
                var row = mainTable.NewRow();
                foreach (DataColumn col in newData.Columns)
                {
                    string columnName = col.ColumnName;
                    row[columnName] = newRow[columnName];
                }
                mainTable.Rows.Add(row);
            }
        }

        private string FindMatchingColumnName(DataTable table, string baseColumnName)
        {
            int suffix = 1;
            string columnName = baseColumnName;

            while (table.Columns.Contains(columnName))
            {
                columnName = $"{baseColumnName}_{++suffix}";
            }

            return columnName;
        }

        private DataTable ToDataTable(IEnumerable<dynamic> data)
        {
            var dt = new DataTable();
            var first = true;

            foreach (var row in data)
            {
                IDictionary<string, object> dict = row as IDictionary<string, object>;
                if (first)
                {
                    first = false;
                    foreach (var col in dict.Keys)
                    {
                        dt.Columns.Add(col);
                    }
                }

                var newRow = dt.NewRow();
                foreach (var col in dict.Keys)
                {
                    newRow[col] = dict[col] ?? DBNull.Value;
                }
                dt.Rows.Add(newRow);
            }

            return dt;
        }
    }

    public class ParameterInfo
    {
        public string Name { get; set; }
        public string DataType { get; set; }
        public string Mode { get; set; }
        public string IsNullable { get; set; }
        public string DefaultValue { get; set; }

        public bool IsOptional => !string.IsNullOrEmpty(DefaultValue) || IsNullable == "YES";

        public DbType GetDbType()
        {
            return DataType.ToLower() switch
            {
                "varchar" => DbType.String,
                "nvarchar" => DbType.String,
                "int" => DbType.Int32,
                "decimal" => DbType.Decimal,
                "datetime" => DbType.DateTime,
                "bit" => DbType.Boolean,
                "float" => DbType.Double,
                "bigint" => DbType.Int64,
                "smallint" => DbType.Int16,
                "date" => DbType.Date,
                "time" => DbType.Time,
                "money" => DbType.Currency,
                _ => DbType.String
            };
        }
    }

}