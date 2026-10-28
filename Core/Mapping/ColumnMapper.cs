using Syncro.Core.Models.Enums;

namespace Syncro.Core.Mapping;

public static class ColumnMapper
{
    /// <summary>Wert der Spalte issues.status → Enum. Unbekannte Werte landen im Backlog.</summary>
    public static Column ToColumn(this string? status)
    {
        return Enum.TryParse<Column>(status, out var column) ? column : Column.Backlog;
    }

    /// <summary>Enum → Wert der Spalte issues.status (entspricht dem Enum-Namen).</summary>
    public static string ToStatus(this Column column) => column.ToString();

    public static string ToDisplayName(this Column column)
    {
        return column switch
        {
            Column.Todo => "To do",
            Column.InProgress => "In Arbeit",
            Column.Review => "Review",
            Column.Done => "Erledigt",
            _ => "Backlog"
        };
    }
}
