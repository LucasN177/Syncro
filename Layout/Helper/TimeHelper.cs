namespace Syncro.Layout.Helper;

public static class TimeHelper
{
    /// <summary>Kurze relative Zeitangabe, z. B. „vor 5 Min.“ oder „gestern“.</summary>
    public static string Relative(DateTime timestamp)
    {
        var local = timestamp.Kind == DateTimeKind.Utc ? timestamp.ToLocalTime() : timestamp;
        var diff = DateTime.Now - local;

        if (diff.TotalMinutes < 1)
            return "gerade eben";
        if (diff.TotalHours < 1)
            return $"vor {(int)diff.TotalMinutes} Min.";
        if (diff.TotalDays < 1 && local.Date == DateTime.Today)
            return $"vor {(int)diff.TotalHours} Std.";
        if (local.Date == DateTime.Today.AddDays(-1))
            return "gestern";
        if (diff.TotalDays < 7)
            return $"vor {(int)Math.Ceiling(diff.TotalDays)} Tagen";
        return local.ToString("dd.MM.yyyy");
    }

    /// <summary>„Heute · 09:00“, „Morgen · 14:00“ oder „Fr, 30.10. · 10:00“.</summary>
    public static string Upcoming(DateTime start)
    {
        var day = start.Date == DateTime.Today ? "Heute"
            : start.Date == DateTime.Today.AddDays(1) ? "Morgen"
            : start.ToString("ddd, dd.MM.");
        return $"{day} · {start:HH:mm}";
    }
}
