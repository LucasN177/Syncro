namespace Syncro.Core.Models;

public class Issue
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Priority { get; set; } = "Medium";
    public string Type { get; set; } = "Issue";
    public string Assignee { get; set; } = "";
    public List<string> Tags { get; set; } = new();
    public int? Progress { get; set; }
    public int CommentCount { get; set; }
    public bool HasAttachment { get; set; }
    public string DueDate { get; set; } = "";
    public string ColumnId { get; set; } = "";
}