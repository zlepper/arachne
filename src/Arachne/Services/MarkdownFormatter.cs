
namespace Arachne.Services;

public class MarkdownFormatter : IMarkdownFormatter
{
    public Task<string> GenerateMarkdownReportAsync(List<QueryResult> results, OutputConfiguration outputConfig)
    {
        var report = new StringBuilder();
        
        // Header
        report.AppendLine("# Cross-Database Query Results");
        report.AppendLine();
        
        if (outputConfig.IncludeTimestamp)
        {
            report.AppendLine($"**Executed:** {DateTime.Now.ToString(outputConfig.DateTimeFormat)}");
            report.AppendLine();
        }
        
        // Table of Contents
        report.AppendLine("## Table of Contents");
        report.AppendLine("- [Summary](#summary)");
        report.AppendLine("- [Results by Server](#results-by-server)");
        report.AppendLine("- [Detailed Statistics](#detailed-statistics)");
        report.AppendLine();
        
        // Summary section
        GenerateSummarySection(report, results, outputConfig);
        
        // Results by server
        report.AppendLine("## Results by Server");
        report.AppendLine();
        
        var groupedResults = results.GroupBy(r => r.ServerName).ToList();
        
        foreach (var serverGroup in groupedResults)
        {
            var serverResults = serverGroup.ToList();
            report.AppendLine($"### {EscapeMarkdown(serverGroup.Key)}");
            report.AppendLine();
            
            foreach (var result in serverResults)
            {
                if (!outputConfig.ShowEmptyResults && !result.HasData)
                    continue;
                    
                GenerateDatabaseSection(report, result, outputConfig);
            }
        }
        
        // Detailed statistics
        GenerateDetailedStatistics(report, results, outputConfig);
        
        return Task.FromResult(report.ToString());
    }
    
    public string FormatDataTableAsMarkdown(DataTable dataTable, OutputConfiguration outputConfig)
    {
        if (dataTable.Rows.Count == 0)
            return "*No data returned.*\n";
            
        var markdown = new StringBuilder();
        
        // Table header
        var headers = dataTable.Columns.Cast<DataColumn>()
            .Select(col => EscapeMarkdown(col.ColumnName))
            .ToArray();
            
        markdown.AppendLine("| " + string.Join(" | ", headers) + " |");
        markdown.AppendLine("|" + string.Join("|", headers.Select(_ => "---")) + "|");
        
        // Limit rows based on configuration
        var rowsToShow = Math.Min(dataTable.Rows.Count, outputConfig.MaxRowsPerDatabase);
        
        // Table rows
        for (int i = 0; i < rowsToShow; i++)
        {
            var row = dataTable.Rows[i];
            var rowData = new string[dataTable.Columns.Count];
            
            for (int j = 0; j < dataTable.Columns.Count; j++)
            {
                var value = row[j];
                rowData[j] = EscapeMarkdown(FormatCellValue(value, outputConfig));
            }
            
            markdown.AppendLine("| " + string.Join(" | ", rowData) + " |");
        }
        
        // Add truncation message if needed
        if (dataTable.Rows.Count > outputConfig.MaxRowsPerDatabase)
        {
            markdown.AppendLine();
            markdown.AppendLine($"*... and {dataTable.Rows.Count - outputConfig.MaxRowsPerDatabase} more rows*");
        }
        
        markdown.AppendLine();
        return markdown.ToString();
    }
    
    private void GenerateSummarySection(StringBuilder report, List<QueryResult> results, OutputConfiguration outputConfig)
    {
        report.AppendLine("## Summary");
        report.AppendLine();
        
        var groupedResults = results.GroupBy(r => r.ServerName).ToList();
        var serverCount = groupedResults.Count;
        var successfulServers = groupedResults.Count(g => g.Any(r => !r.HasError));
        var totalDatabases = results.Count;
        var databasesWithResults = results.Count(r => r.HasData);
        var databasesWithErrors = results.Count(r => r.HasError);
        var avgExecutionTime = results.Count > 0 ? results.Average(r => r.ExecutionTime.TotalSeconds) : 0;
        var totalExecutionTime = results.Sum(r => r.ExecutionTime.TotalSeconds);
        
        report.AppendLine("| Metric | Value |");
        report.AppendLine("|--------|-------|");
        report.AppendLine($"| **Servers processed** | {successfulServers}/{serverCount} successful |");
        report.AppendLine($"| **Total databases** | {totalDatabases} |");
        report.AppendLine($"| **Databases with results** | {databasesWithResults} |");
        report.AppendLine($"| **Databases with errors** | {databasesWithErrors} |");
        report.AppendLine($"| **Total rows returned** | {results.Where(r => r.HasData).Sum(r => r.RowCount)} |");
        report.AppendLine($"| **Total execution time** | {totalExecutionTime:F1}s |");
        report.AppendLine($"| **Average execution time** | {avgExecutionTime:F2}s |");
        report.AppendLine();
        
        // Query version usage
        var queryUsage = results
            .Where(r => r.SuccessfulQuery != null)
            .GroupBy(r => r.SuccessfulQuery!.Name)
            .Where(g => g.Key != null)
            .ToDictionary(g => g.Key!, g => g.Count());
            
        if (queryUsage.Any())
        {
            report.AppendLine("### Query Version Usage");
            report.AppendLine();
            report.AppendLine("| Query Version | Databases |");
            report.AppendLine("|---------------|-----------|");
            foreach (var usage in queryUsage.OrderBy(u => u.Key))
            {
                report.AppendLine($"| {EscapeMarkdown(usage.Key ?? "")} | {usage.Value} |");
            }
            report.AppendLine();
        }
    }
    
    private void GenerateDatabaseSection(StringBuilder report, QueryResult result, OutputConfiguration outputConfig)
    {
        var queryInfo = outputConfig.ShowQueryVersion && result.SuccessfulQuery != null 
            ? $" (Query: {result.SuccessfulQuery.Name})" 
            : "";
            
        report.AppendLine($"#### {EscapeMarkdown(result.DatabaseName)}{queryInfo}");
        report.AppendLine();
        
        if (result.HasError)
        {
            report.AppendLine("**Status:** ❌ Error");
            report.AppendLine($"**Error:** {EscapeMarkdown(result.ErrorMessage ?? "Unknown error")}");
            report.AppendLine();
            return;
        }
        
        report.AppendLine($"**Status:** ✅ Success");
        report.AppendLine($"**Rows:** {result.RowCount}");
        report.AppendLine($"**Execution Time:** {result.ExecutionTime.TotalSeconds:F2}s");
        
        if (result.FailedQueryNames.Count > 0)
        {
            report.AppendLine($"**Failed Queries:** {string.Join(", ", result.FailedQueryNames.Select(EscapeMarkdown))}");
        }
        
        report.AppendLine();
        
        if (result.HasData)
        {
            report.AppendLine("**Data:**");
            report.AppendLine();
            report.Append(FormatDataTableAsMarkdown(result.Data!, outputConfig));
        }
        else
        {
            report.AppendLine("*No data returned.*");
            report.AppendLine();
        }
    }
    
    private void GenerateDetailedStatistics(StringBuilder report, List<QueryResult> results, OutputConfiguration outputConfig)
    {
        report.AppendLine("## Detailed Statistics");
        report.AppendLine();
        
        var groupedResults = results.GroupBy(r => r.ServerName).ToList();
        
        report.AppendLine("### Server Statistics");
        report.AppendLine();
        report.AppendLine("| Server | Databases | Success | Errors | Total Rows | Avg Time (s) |");
        report.AppendLine("|--------|-----------|---------|--------|------------|--------------|");
        
        foreach (var serverGroup in groupedResults)
        {
            var serverResults = serverGroup.ToList();
            var successCount = serverResults.Count(r => !r.HasError);
            var errorCount = serverResults.Count(r => r.HasError);
            var totalRows = serverResults.Where(r => r.HasData).Sum(r => r.RowCount);
            var avgTime = serverResults.Count > 0 ? serverResults.Average(r => r.ExecutionTime.TotalSeconds) : 0;
            
            report.AppendLine($"| {EscapeMarkdown(serverGroup.Key)} | {serverResults.Count} | {successCount} | {errorCount} | {totalRows} | {avgTime:F2} |");
        }
        report.AppendLine();
        
        // Error analysis
        var errorResults = results.Where(r => r.HasError).ToList();
        if (errorResults.Any())
        {
            report.AppendLine("### Error Analysis");
            report.AppendLine();
            
            var errorGroups = errorResults
                .GroupBy(r => r.ErrorMessage ?? "Unknown error")
                .OrderByDescending(g => g.Count())
                .ToList();
                
            report.AppendLine("| Error | Count | Affected Databases |");
            report.AppendLine("|-------|-------|--------------------|");
            
            foreach (var errorGroup in errorGroups)
            {
                var databases = string.Join(", ", errorGroup.Select(r => r.DatabaseName).Take(3));
                if (errorGroup.Count() > 3)
                {
                    databases += $", ... and {errorGroup.Count() - 3} more";
                }
                
                report.AppendLine($"| {EscapeMarkdown(errorGroup.Key)} | {errorGroup.Count()} | {EscapeMarkdown(databases)} |");
            }
            report.AppendLine();
        }
        
        // Performance analysis
        report.AppendLine("### Performance Analysis");
        report.AppendLine();
        
        var successfulResults = results.Where(r => !r.HasError).ToList();
        if (successfulResults.Any())
        {
            var minTime = successfulResults.Min(r => r.ExecutionTime.TotalSeconds);
            var maxTime = successfulResults.Max(r => r.ExecutionTime.TotalSeconds);
            var medianTime = successfulResults
                .OrderBy(r => r.ExecutionTime.TotalSeconds)
                .Skip(successfulResults.Count / 2)
                .First().ExecutionTime.TotalSeconds;
                
            report.AppendLine("| Metric | Value |");
            report.AppendLine("|--------|-------|");
            report.AppendLine($"| **Fastest query** | {minTime:F2}s |");
            report.AppendLine($"| **Slowest query** | {maxTime:F2}s |");
            report.AppendLine($"| **Median time** | {medianTime:F2}s |");
            
            var slowQueries = successfulResults
                .Where(r => r.ExecutionTime.TotalSeconds > medianTime * 2)
                .OrderByDescending(r => r.ExecutionTime.TotalSeconds)
                .Take(5)
                .ToList();
                
            if (slowQueries.Any())
            {
                report.AppendLine();
                report.AppendLine("**Slowest queries:**");
                foreach (var query in slowQueries)
                {
                    report.AppendLine($"- {EscapeMarkdown(query.ServerName)}.{EscapeMarkdown(query.DatabaseName)}: {query.ExecutionTime.TotalSeconds:F2}s");
                }
            }
        }
        
        report.AppendLine();
        report.AppendLine("---");
        report.AppendLine($"*Report generated on {DateTime.Now.ToString(outputConfig.DateTimeFormat)}*");
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
    
    private static string EscapeMarkdown(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;
            
        // Escape markdown special characters
        return text
            .Replace("\\", "\\\\")
            .Replace("`", "\\`")
            .Replace("*", "\\*")
            .Replace("_", "\\_")
            .Replace("{", "\\{")
            .Replace("}", "\\}")
            .Replace("[", "\\[")
            .Replace("]", "\\]")
            .Replace("(", "\\(")
            .Replace(")", "\\)")
            .Replace("#", "\\#")
            .Replace("+", "\\+")
            .Replace("-", "\\-")
            .Replace(".", "\\.")
            .Replace("!", "\\!")
            .Replace("|", "\\|");
    }
}