using MudBlazor;

namespace Syncro.Layout.UI_Models;

public class CalendarCategoryFilter
{
    public string Name { get; set; } = "";
    public Color Color { get; set; }
    public string Hex { get; set; } = "#6366F1";
    public bool Active { get; set; } = true;
}