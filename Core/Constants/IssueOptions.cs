namespace Syncro.Core.Constants;

/// <summary>Werte entsprechen den Check-Constraints auf issues.type und issues.priority.</summary>
public static class IssueOptions
{
    public static readonly IReadOnlyList<string> Types = ["Feature", "Bug", "Task", "Improvement", "Documentation"];

    public static readonly IReadOnlyList<string> Priorities = ["Low", "Medium", "High", "Critical"];

    public static string TypeDisplayName(string type) => type switch
    {
        "Task" => "Aufgabe",
        "Improvement" => "Verbesserung",
        "Documentation" => "Dokumentation",
        _ => type
    };

    public static string PriorityDisplayName(string priority) => priority switch
    {
        "Low" => "Niedrig",
        "Medium" => "Mittel",
        "High" => "Hoch",
        "Critical" => "Kritisch",
        _ => priority
    };
}
