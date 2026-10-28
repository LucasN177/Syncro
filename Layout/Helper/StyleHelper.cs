using MudBlazor;
using Syncro.Core.Models.Enums;

namespace Syncro.Layout.Helper;

public static class StyleHelper
{
    // Halbtransparente Töne, damit die Spalten im Light- und Dark-Mode funktionieren
    public static string GetColumnStyle(Column column) => column switch
    {
        Column.Backlog    => "background: rgba(100, 116, 139, 0.06); ",
        Column.Todo       => "background: rgba(2, 132, 199, 0.06); ",
        Column.InProgress => "background: rgba(217, 119, 6, 0.06); ",
        Column.Review     => "background: rgba(37, 99, 235, 0.06); ",
        Column.Done       => "background: rgba(22, 163, 74, 0.06); ",
        _                 => ""
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

    /// <summary>Bis zu zwei Initialen aus einem Namen oder einer E-Mail-Adresse.</summary>
    public static string GetInitials(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "?";

        var parts = name.Split('@')[0]
            .Split([' ', '.', '_', '-'], StringSplitOptions.RemoveEmptyEntries);

        return parts.Length switch
        {
            0 => "?",
            1 => parts[0][..Math.Min(2, parts[0].Length)].ToUpperInvariant(),
            _ => string.Concat(parts.Take(2).Select(part => char.ToUpperInvariant(part[0])))
        };
    }
}
