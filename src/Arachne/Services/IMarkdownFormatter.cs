using System.Data;
using Arachne.Models;

namespace Arachne.Services;

public interface IMarkdownFormatter
{
    Task<string> GenerateMarkdownReportAsync(List<QueryResult> results, OutputConfiguration outputConfig);
    string FormatDataTableAsMarkdown(DataTable dataTable, OutputConfiguration outputConfig);
}