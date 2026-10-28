using System.Text;
using Syncro.Core.Constants;
using Syncro.Core.Mapping;
using Syncro.Core.Models.Database;

namespace Syncro.Layout.Helper;

public static class CsvExporter
{
    /// <summary>Issues als CSV mit Semikolon und BOM, damit Excel Umlaute korrekt erkennt.</summary>
    public static string ExportIssues(IEnumerable<IssueDto> issues, IReadOnlyList<ProjectDto> projects,
        IReadOnlyDictionary<Guid, ProfileDto> profiles)
    {
        var csv = new StringBuilder("﻿");
        csv.AppendLine("Projekt;Nummer;Titel;Typ;Priorität;Status;Zugewiesen;Fällig;Fortschritt;Tags;Erstellt");

        foreach (var issue in issues
                     .OrderBy(issue => projects.FirstOrDefault(p => p.Id == issue.ProjectId)?.Name)
                     .ThenBy(issue => issue.Number))
        {
            var project = projects.FirstOrDefault(p => p.Id == issue.ProjectId);
            var assignee = issue.AssigneeId is { } assigneeId ? profiles.GetValueOrDefault(assigneeId) : null;

            csv.AppendLine(string.Join(';',
                Escape(project?.Name),
                Escape(issue.ToIssueKey(project?.Key)),
                Escape(issue.Title),
                Escape(IssueOptions.TypeDisplayName(issue.Type)),
                Escape(IssueOptions.PriorityDisplayName(issue.Priority)),
                Escape(issue.Status.ToColumn().ToDisplayName()),
                Escape(assignee?.DisplayName),
                Escape(issue.DueDate?.ToString("dd.MM.yyyy")),
                Escape(issue.Progress?.ToString()),
                Escape(string.Join(", ", issue.Tags)),
                Escape(issue.CreatedAt.ToLocalTime().ToString("dd.MM.yyyy"))));
        }

        return csv.ToString();
    }

    private static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;
        return value.IndexOfAny([';', '"', '\n', '\r']) >= 0
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
    }
}
