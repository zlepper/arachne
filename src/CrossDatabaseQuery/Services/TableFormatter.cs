using System.Data;
using System.Text;
using CrossDatabaseQuery.Models;
using ConsoleTableExt;

namespace CrossDatabaseQuery.Services;

public interface ITableFormatter
{
    string FormatResults(List<QueryResult> results, OutputConfiguration outputConfig);
    string FormatTable(DataTable dataTable, OutputConfiguration outputConfig);
}

public class TableFormatter : ITableFormatter
{
    public string FormatResults(List<QueryResult> results, OutputConfiguration outputConfig)
    {
        var output = new StringBuilder();
        
        output.AppendLine("========================================");
        output.AppendLine("Cross-Database Query Results");
        output.AppendLine("========================================");
        
        var groupedResults = results.GroupBy(r => r.ServerName).ToList();
        
        if (outputConfig.IncludeTimestamp)
        {
            output.AppendLine($"Executed: {DateTime.Now.ToString(outputConfig.DateTimeFormat)}");
            output.AppendLine();
        }

        foreach (var serverGroup in groupedResults)
        {
            var serverResults = serverGroup.ToList();
            var firstResult = serverResults.First();
            
            output.AppendLine($"┌─────────────────────────────────────────┐");
            output.AppendLine($"│ {serverGroup.Key.PadRight(39)} │");
            output.AppendLine($"└─────────────────────────────────────────┘");
            output.AppendLine();

            foreach (var result in serverResults)
            {
                if (!outputConfig.ShowEmptyResults && !result.HasData)
                    continue;

                if (result.HasError)
                {
                    output.AppendLine($"Database: {result.DatabaseName} [ERROR]");
                    output.AppendLine($"❌ {result.ErrorMessage}");
                    output.AppendLine();
                    continue;
                }

                var queryInfo = outputConfig.ShowQueryVersion && result.SuccessfulQuery != null 
                    ? $" [Query: {result.SuccessfulQuery.Name}]" 
                    : "";
                    
                output.AppendLine($"Database: {result.DatabaseName}{queryInfo} ({result.RowCount} rows)");
                
                if (result.FailedQueryNames.Count > 0)
                {
                    output.AppendLine($"⚠️  Queries failed: {string.Join(", ", result.FailedQueryNames)}");
                }

                if (result.HasData)
                {
                    output.AppendLine(FormatTable(result.Data!, outputConfig));
                }
                else
                {
                    output.AppendLine("No data returned.");
                }
                
                output.AppendLine();
            }
        }

        // Summary
        output.AppendLine("========================================");
        output.AppendLine("Summary:");
        
        var serverCount = groupedResults.Count;
        var successfulServers = groupedResults.Count(g => g.Any(r => !r.HasError));
        output.AppendLine($"- Servers processed: {successfulServers}/{serverCount} successful");
        
        var totalDatabases = results.Count;
        var databasesWithResults = results.Count(r => r.HasData);
        output.AppendLine($"- Databases discovered: {totalDatabases}");
        output.AppendLine($"- Databases with results: {databasesWithResults}");
        
        // Query version usage
        var queryUsage = results
            .Where(r => r.SuccessfulQuery != null)
            .GroupBy(r => r.SuccessfulQuery!.Name)
            .ToDictionary(g => g.Key, g => g.Count());
            
        if (queryUsage.Any())
        {
            output.AppendLine("- Query version usage:");
            foreach (var usage in queryUsage.OrderBy(u => u.Key))
            {
                output.AppendLine($"  • {usage.Key}: {usage.Value} database(s)");
            }
        }
        
        var totalExecutionTime = results.Sum(r => r.ExecutionTime.TotalSeconds);
        output.AppendLine($"- Total execution time: {totalExecutionTime:F1}s");
        output.AppendLine("========================================");

        return output.ToString();
    }

    public string FormatTable(DataTable dataTable, OutputConfiguration outputConfig)
    {
        if (dataTable.Rows.Count == 0)
            return "No data returned.";

        // Convert DataTable to List of List<object> for ConsoleTableExt
        var tableData = new List<List<object>>();
        
        // Add headers as first row
        var headers = dataTable.Columns
            .Cast<DataColumn>()
            .Select(column => (object)column.ColumnName)
            .ToList();
        tableData.Add(headers);
        
        // Limit rows based on configuration
        var rowsToShow = Math.Min(dataTable.Rows.Count, outputConfig.MaxRowsPerDatabase);
        
        // Add data rows
        for (int i = 0; i < rowsToShow; i++)
        {
            var row = dataTable.Rows[i];
            var rowData = new List<object>();
            
            foreach (DataColumn column in dataTable.Columns)
            {
                var value = row[column];
                var formattedValue = FormatCellValue(value, outputConfig);
                rowData.Add(formattedValue);
            }
            
            tableData.Add(rowData);
        }
        
        // Create table using ConsoleTableExt
        var table = ConsoleTableBuilder
            .From(tableData)
            .WithFormat(ConsoleTableBuilderFormat.Alternative)
            .Export()
            .ToString();
            
        // Add truncation message if needed
        if (dataTable.Rows.Count > outputConfig.MaxRowsPerDatabase)
        {
            table += $"\n... and {dataTable.Rows.Count - outputConfig.MaxRowsPerDatabase} more rows";
        }

        return table;
    }

    private static string FormatCellValue(object? value, OutputConfiguration outputConfig)
    {
        return value switch
        {
            null => outputConfig.NullDisplayValue,
            DBNull => outputConfig.NullDisplayValue,
            DateTime dateTime => dateTime.ToString(outputConfig.DateTimeFormat),
            _ => value.ToString() ?? outputConfig.NullDisplayValue
        };
    }
}