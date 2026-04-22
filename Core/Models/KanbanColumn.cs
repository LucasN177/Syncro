using MudBlazor;

namespace Syncro.Core.Models;

public class KanbanColumn
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public Color ChipColor { get; set; }
    public Color BadgeColor { get; set; }
    public List<KanbanCard> Cards { get; set; } = new();
}