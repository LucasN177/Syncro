using MudBlazor;
using Syncro.Core.Models.Enums;

namespace Syncro.Core.Models;

public class KanbanCard
{
    public Guid IssueId { get; set; }
    public Guid ProjectId { get; set; }

    /// <summary>Anzeige-Schlüssel, z. B. SYN-12.</summary>
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public string Priority { get; set; } = "Medium";
    public string Type { get; set; } = "Task";
    public Guid? AssigneeId { get; set; }
    public string Assignee { get; set; } = "";
    public Color AvatarColor { get; set; } = Color.Primary;
    public string AvatarHex { get; set; } = "#0f766e";
    public List<string> Tags { get; set; } = new();
    public int? Progress { get; set; }
    public int CommentCount { get; set; }
    public bool HasAttachment { get; set; }
    public DateTime? DueDate { get; set; }
    public double Position { get; set; }
    public Column Column { get; set; }

    public bool IsOverdue => DueDate is { } due && due.Date < DateTime.Today && Column != Column.Done;
}
