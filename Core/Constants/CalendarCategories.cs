using MudBlazor;
using Syncro.Layout.UI_Models;

namespace Syncro.Core.Constants;

/// <summary>Kategorien entsprechen dem Check-Constraint auf events.category.</summary>
public static class CalendarCategories
{
    public const string Meeting = "Meeting";
    public const string Review = "Review";
    public const string Release = "Release";
    public const string Personal = "Personal";
    public const string Deadline = "Deadline";

    // Neue Instanzen je Aufruf, da der Filterzustand (Active) pro Seite gehalten wird
    public static List<CalendarCategoryFilter> Create() =>
    [
        new() { Name = Meeting,  Color = Color.Primary,   Hex = "#6366F1" },
        new() { Name = Review,   Color = Color.Warning,   Hex = "#F59E0B" },
        new() { Name = Release,  Color = Color.Success,   Hex = "#10B981" },
        new() { Name = Personal, Color = Color.Secondary, Hex = "#8B5CF6" },
        new() { Name = Deadline, Color = Color.Error,     Hex = "#EF4444" }
    ];

    public static string DisplayName(string category) => category switch
    {
        Personal => "Privat",
        _ => category
    };
}
