using MudBlazor;

namespace Syncro.Layout.Helper;

public static class StyleHelper
{
    public static string GetColumnStyle(string columnId) => columnId switch
    {
        "backlog"    => "background: #F8FAFC; ",
        "todo"       => "background: #EFF6FF; ",
        "inprogress" => "background: #FFFBEB; ",
        "done"       => "background: #F0FDF4; ",
        _            => ""
    };

    public static Color GetPriorityColor(string priority) => priority switch
    {
        "Critical" => Color.Error,
        "High"     => Color.Warning,
        "Medium"   => Color.Info,
        "Low"      => Color.Success,
        _          => Color.Default
    };

    public static string GetPriorityIcon(string priority) => priority switch
    {
        "Critical" => Icons.Material.Filled.KeyboardDoubleArrowUp,
        "High"     => Icons.Material.Filled.KeyboardArrowUp,
        "Medium"   => Icons.Material.Filled.DragHandle,
        "Low"      => Icons.Material.Filled.KeyboardArrowDown,
        _          => Icons.Material.Filled.DragHandle
    };

    public static Color GetProgressColor(int value) => value switch
    {
        100   => Color.Success,
        >= 60 => Color.Info,
        >= 30 => Color.Warning,
        _     => Color.Error
    };
}