using MudBlazor;
using Syncro.Core.Models;
using Syncro.Core.Models.Enums;
using Syncro.Layout.UI_Models;

namespace Syncro.FakeData;

public static class SampleData
{
    public static readonly List<KanbanCard> Cards = new()
    {
        new()
        {
            Id = "KAN-001",
            Title = "Benutzeranmeldung implementieren",
            Priority = "High",
            Type = "Feature",
            Assignee = "Max Mustermann",
            AvatarColor = Color.Info,
            Tags = new() { "Backend", "Auth" },
            Progress = 25,
            CommentCount = 4,
            HasAttachment = true,
            DueDate = "2026-05-05",
            Column = Column.Todo
        },
        new()
        {
            Id = "KAN-002",
            Title = "Fehler beim Datei-Upload beheben",
            Priority = "Critical",
            Type = "Bug",
            Assignee = "Anna Schmidt",
            AvatarColor = Color.Success,
            Tags = new() { "Bug", "Upload" },
            Progress = 80,
            CommentCount = 7,
            HasAttachment = true,
            DueDate = "2026-05-01",
            Column = Column.InProgress
        },
        new()
        {
            Id = "KAN-003",
            Title = "Dashboard UI überarbeiten",
            Priority = "Medium",
            Type = "Task",
            Assignee = "Lukas Weber",
            AvatarColor = Color.Success,
            Tags = new() { "UI", "Design" },
            Progress = 50,
            CommentCount = 2,
            HasAttachment = false,
            DueDate = "2026-05-10",
            Column = Column.Review
        },
        new()
        {
            Id = "KAN-004",
            Title = "API-Dokumentation erstellen",
            Priority = "Low",
            Type = "Documentation",
            Assignee = "Sophie Müller",
            AvatarColor = Color.Warning,
            Tags = new() { "Docs", "API" },
            Progress = 100,
            CommentCount = 1,
            HasAttachment = true,
            DueDate = "2026-04-28",
            Column = Column.Done
        },
        new()
        {
            Id = "KAN-005",
            Title = "Performance der Datenbank optimieren",
            Priority = "High",
            Type = "Improvement",
            Assignee = "David Fischer",
            AvatarColor = Color.Tertiary,
            Tags = new() { "Database", "Performance" },
            Progress = 60,
            CommentCount = 5,
            HasAttachment = false,
            DueDate = "2026-05-15",
            Column = Column.Todo
        },
        new()
        {
            Id = "KAN-006",
            Title = "Dark Mode hinzufügen",
            Priority = "Medium",
            Type = "Feature",
            Assignee = "Julia Becker",
            AvatarColor = Color.Warning,
            Tags = new() { "UI", "Enhancement" },
            Progress = null,
            CommentCount = 0,
            HasAttachment = false,
            DueDate = "2026-05-20",
            Column = Column.Backlog
        }
    };
    
    public static readonly List<KanbanColumn> Columns = new()
    {
        new KanbanColumn
        {
            Id = Column.Backlog, Title = "BACKLOG", ChipColor = Color.Default, BadgeColor = Color.Default,
            Cards = Cards.Where(x => x.Column == Column.Backlog).ToList(),
        },
        new KanbanColumn
        {
            Id = Column.Todo, Title = "TO DO", ChipColor = Color.Info, BadgeColor = Color.Info,
            Cards = Cards.Where(x => x.Column == Column.Todo).ToList(),
        },
        new KanbanColumn
        {
            Id = Column.InProgress, Title = "IN PROGRESS", ChipColor = Color.Warning, BadgeColor = Color.Warning,
            Cards = Cards.Where(x => x.Column == Column.InProgress).ToList(),
        },
        new KanbanColumn
        {
            Id = Column.Review, Title = "Review", ChipColor = Color.Info, BadgeColor = Color.Info,
            Cards = Cards.Where(x => x.Column == Column.Review).ToList(),
        },
        new KanbanColumn
        {
            Id = Column.Done, Title = "DONE", ChipColor = Color.Success, BadgeColor = Color.Success,
            Cards = Cards.Where(x => x.Column == Column.Done).ToList(),
        },
    };
    
    public static readonly List<CalendarEvent> Events = new()
    {
        new() { Title = "Sprint Planning",       Category = "Meeting",  Start = DateTime.Today.AddDays(0).Date.AddHours(9),  End = DateTime.Today.AddDays(0).Date.AddHours(10),  Location = "Room A", Attendees = new() { "Lucas M.", "Anna K.", "Tim R." }, Description = "Sprint 5 kick-off planning session." },
        new() { Title = "Design Review",         Category = "Review",   Start = DateTime.Today.AddDays(1).Date.AddHours(14), End = DateTime.Today.AddDays(1).Date.AddHours(15),  Location = "Zoom",   Attendees = new() { "Sara B.", "Anna K." },             Description = "Review new onboarding screens." },
        new() { Title = "v2.0 Release",          Category = "Release",  Start = DateTime.Today.AddDays(3).Date.AddHours(10), End = DateTime.Today.AddDays(3).Date.AddHours(11),  Location = "",       Attendees = new() { "Lucas M.", "Tim R." },              Description = "Production release of v2.0." },
        new() { Title = "1:1 with Anna",         Category = "Meeting",  Start = DateTime.Today.AddDays(2).Date.AddHours(11), End = DateTime.Today.AddDays(2).Date.AddHours(12),  Location = "Office", Attendees = new() { "Anna K." },                        Description = "" },
        new() { Title = "API Deadline",          Category = "Deadline", Start = DateTime.Today.AddDays(5).Date.AddHours(18), End = DateTime.Today.AddDays(5).Date.AddHours(18),  Location = "",       Attendees = new(),                                      Description = "Public API documentation must be finalized." },
        new() { Title = "Team Retrospective",    Category = "Meeting",  Start = DateTime.Today.AddDays(7).Date.AddHours(15), End = DateTime.Today.AddDays(7).Date.AddHours(16),  Location = "Room B", Attendees = new() { "Lucas M.", "Anna K.", "Tim R.", "Sara B." }, Description = "" },
        new() { Title = "Doctor Appointment",    Category = "Personal", Start = DateTime.Today.AddDays(4).Date.AddHours(8),  End = DateTime.Today.AddDays(4).Date.AddHours(9),   Location = "City Center", Attendees = new(),                               Description = "" },
        new() { Title = "Code Review Session",   Category = "Review",   Start = DateTime.Today.AddDays(6).Date.AddHours(10), End = DateTime.Today.AddDays(6).Date.AddHours(11),  Location = "Zoom",   Attendees = new() { "Tim R.", "Lucas M." },              Description = "Review EF Core migration PR." },
        new() { Title = "Stakeholder Demo",      Category = "Meeting",  Start = DateTime.Today.AddDays(9).Date.AddHours(13), End = DateTime.Today.AddDays(9).Date.AddHours(14),  Location = "Boardroom", Attendees = new() { "Lucas M.", "Anna K." },          Description = "Demo of sprint results to stakeholders." },
        new() { Title = "Hotfix Deadline",       Category = "Deadline", Start = DateTime.Today.AddDays(2).Date.AddHours(17), End = DateTime.Today.AddDays(2).Date.AddHours(17),  Location = "",       Attendees = new(),                                      Description = "Auth hotfix must be deployed." },
        new() { Title = "Gym",                   Category = "Personal", Start = DateTime.Today.AddDays(1).Date.AddHours(7),  End = DateTime.Today.AddDays(1).Date.AddHours(8),   Location = "FitX",   Attendees = new(),                                      Description = "" },
        new() { Title = "Architecture Review",   Category = "Review",   Start = DateTime.Today.AddDays(10).Date.AddHours(9),End = DateTime.Today.AddDays(10).Date.AddHours(11),  Location = "Room A", Attendees = new() { "Lucas M.", "Tim R.", "Sara B." },   Description = "Review new microservice boundaries." },
    };
    
    public static readonly List<CalendarCategoryFilter> Categories = new()
    {
        new() { Name = "Meeting",  Color = Color.Primary,   Hex = "#6366F1" },
        new() { Name = "Review",   Color = Color.Warning,   Hex = "#F59E0B" },
        new() { Name = "Release",  Color = Color.Success,   Hex = "#10B981" },
        new() { Name = "Personal", Color = Color.Secondary, Hex = "#8B5CF6" },
        new() { Name = "Deadline", Color = Color.Error,     Hex = "#EF4444" },
    };
}