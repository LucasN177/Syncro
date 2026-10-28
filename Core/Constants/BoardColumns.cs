using MudBlazor;
using Syncro.Core.Mapping;
using Syncro.Core.Models;
using Syncro.Core.Models.Enums;

namespace Syncro.Core.Constants;

public static class BoardColumns
{
    public static List<KanbanColumn> Create() =>
    [
        CreateColumn(Column.Backlog, Color.Default),
        CreateColumn(Column.Todo, Color.Info),
        CreateColumn(Column.InProgress, Color.Warning),
        CreateColumn(Column.Review, Color.Secondary),
        CreateColumn(Column.Done, Color.Success)
    ];

    private static KanbanColumn CreateColumn(Column id, Color color) => new()
    {
        Id = id,
        Title = id.ToDisplayName().ToUpperInvariant(),
        ChipColor = color,
        BadgeColor = color
    };
}
