using MudBlazor;
using Syncro.Core.Models.Enums;

namespace Syncro.Core.Models;

public class KanbanColumn
{
    public Column Id { get; set; }
    public string Title { get; set; } = "";
    public Color ChipColor { get; set; }
    public Color BadgeColor { get; set; }
    public List<KanbanCard> Cards { get; set; } = new();
}