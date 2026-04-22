using Syncro.Core.Models;

namespace Syncro.Layout.Helper;

public static class CalendarHelper
{
    private static readonly DateTime CurrentMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
    
    public static List<List<DateTime>> GetCalendarWeeks()
    {
        var weeks = new List<List<DateTime>>();
        var first = CurrentMonth;
        var offset = ((int)first.DayOfWeek + 6) % 7; // Monday = 0
        var start = first.AddDays(-offset);

        for (int w = 0; w < 6; w++)
        {
            var week = Enumerable.Range(0, 7).Select(d => start.AddDays(w * 7 + d)).ToList();
            if (week.Any(d => d.Month == CurrentMonth.Month))
                weeks.Add(week);
        }
        return weeks;
    }

    public static List<CalendarEvent> GetEventsForDay(DateTime day, List<CalendarEvent> events) =>
        events.Where(e => e.Start.Date == day.Date).OrderBy(e => e.Start).ToList();

    public static List<DateTime> GetCurrentWeekDays(DateTime? selectedDay)
    {
        var today = selectedDay ?? DateTime.Today;
        var offset = ((int)today.DayOfWeek + 6) % 7;
        var monday = today.AddDays(-offset);
        return Enumerable.Range(0, 7).Select(d => monday.AddDays(d)).ToList();
    }
}