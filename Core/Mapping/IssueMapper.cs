using Syncro.Core.Models;
using Syncro.Core.Models.Database;

namespace Syncro.Core.Mapping;

public static class IssueMapper
{
    public static string ToIssueKey(this IssueDto issue, string? projectKey) =>
        $"{(string.IsNullOrWhiteSpace(projectKey) ? "PRJ" : projectKey)}-{issue.Number}";

    public static KanbanCard ToKanbanCard(this IssueDto issue, string? projectKey, ProfileDto? assignee)
    {
        return new KanbanCard
        {
            IssueId = issue.Id,
            ProjectId = issue.ProjectId,
            Id = issue.ToIssueKey(projectKey),
            Title = issue.Title,
            Description = issue.Description,
            Priority = issue.Priority,
            Type = issue.Type,
            AssigneeId = issue.AssigneeId,
            Assignee = assignee?.DisplayName ?? string.Empty,
            AvatarHex = assignee?.AvatarColor ?? "#0f766e",
            Tags = issue.Tags.ToList(),
            Progress = issue.Progress,
            CommentCount = issue.CommentCount,
            DueDate = issue.DueDate?.Date,
            Position = issue.Position,
            Column = issue.Status.ToColumn()
        };
    }

    /// <summary>Kopie zum Bearbeiten, damit Änderungen erst nach dem Speichern sichtbar werden.</summary>
    public static IssueDto Copy(this IssueDto issue) => new()
    {
        Id = issue.Id,
        ProjectId = issue.ProjectId,
        Number = issue.Number,
        Title = issue.Title,
        Description = issue.Description,
        Type = issue.Type,
        Priority = issue.Priority,
        Status = issue.Status,
        Position = issue.Position,
        AssigneeId = issue.AssigneeId,
        DueDate = issue.DueDate,
        Progress = issue.Progress,
        Tags = issue.Tags.ToList(),
        CommentCount = issue.CommentCount,
        CreatedBy = issue.CreatedBy,
        CreatedAt = issue.CreatedAt,
        UpdatedAt = issue.UpdatedAt
    };
}
