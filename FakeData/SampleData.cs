using MudBlazor;
using Syncro.Core.Models;
using Syncro.Layout.UI_Models;

namespace Syncro.FakeData;

public static class SampleData
{
    public static readonly List<KanbanColumn> Columns = new()
    {
        new KanbanColumn
        {
            Id = "backlog", Title = "BACKLOG", ChipColor = Color.Default, BadgeColor = Color.Default,
            Cards = new List<KanbanCard>
            {
                new() { Id = "PRJ-041", Title = "Design new onboarding flow for mobile", Priority = "Low", Type = "Design", Assignee = "Sara B.", AvatarColor = Color.Info, Tags = new() { "UX", "Mobile" }, CommentCount = 3, DueDate = "May 12" },
                new() { Id = "PRJ-039", Title = "Investigate performance regression in dashboard queries", Priority = "High", Type = "Bug", Assignee = "Tim R.", AvatarColor = Color.Tertiary, Tags = new() { "Backend", "Perf" }, CommentCount = 7, HasAttachment = true, DueDate = "Apr 28" },
                new() { Id = "PRJ-038", Title = "Write unit tests for auth service", Priority = "Medium", Type = "Task", Assignee = "Lucas M.", AvatarColor = Color.Primary, Tags = new() { "Testing" }, DueDate = "May 5" },
            }
        },
        new KanbanColumn
        {
            Id = "todo", Title = "TO DO", ChipColor = Color.Info, BadgeColor = Color.Info,
            Cards = new List<KanbanCard>
            {
                new() { Id = "PRJ-044", Title = "Implement JWT refresh token rotation", Priority = "High", Type = "Feature", Assignee = "Lucas M.", AvatarColor = Color.Primary, Tags = new() { "Security", "Auth" }, Progress = 0, CommentCount = 2, DueDate = "Apr 25" },
                new() { Id = "PRJ-043", Title = "Create email notification templates", Priority = "Medium", Type = "Task", Assignee = "Anna K.", AvatarColor = Color.Secondary, Tags = new() { "Email" }, HasAttachment = true, DueDate = "Apr 26" },
                new() { Id = "PRJ-042", Title = "Add dark mode support across all pages", Priority = "Low", Type = "Improvement", Assignee = "Sara B.", AvatarColor = Color.Info, Tags = new() { "UI", "Theme" }, CommentCount = 5, DueDate = "May 3" },
            }
        },
        new KanbanColumn
        {
            Id = "inprogress", Title = "IN PROGRESS", ChipColor = Color.Warning, BadgeColor = Color.Warning,
            Cards = new List<KanbanCard>
            {
                new() { Id = "PRJ-040", Title = "Refactor file upload pipeline to use presigned URLs", Priority = "High", Type = "Feature", Assignee = "Tim R.", AvatarColor = Color.Tertiary, Tags = new() { "S3", "Files" }, Progress = 65, CommentCount = 4, HasAttachment = true, DueDate = "Apr 24" },
                new() { Id = "PRJ-037", Title = "Build Kanban board drag & drop interactions", Priority = "High", Type = "Feature", Assignee = "Lucas M.", AvatarColor = Color.Primary, Tags = new() { "UI", "Blazor" }, Progress = 80, CommentCount = 9, DueDate = "Apr 23" },
                new() { Id = "PRJ-035", Title = "Integrate Stripe billing for Pro plan", Priority = "Critical", Type = "Feature", Assignee = "Anna K.", AvatarColor = Color.Secondary, Tags = new() { "Billing", "API" }, Progress = 40, CommentCount = 6, HasAttachment = true, DueDate = "Apr 22" },
            }
        },
        new KanbanColumn
        {
            Id = "done", Title = "DONE", ChipColor = Color.Success, BadgeColor = Color.Success,
            Cards = new List<KanbanCard>
            {
                new() { Id = "PRJ-034", Title = "Setup CI/CD pipeline with GitHub Actions", Priority = "High", Type = "DevOps", Assignee = "Tim R.", AvatarColor = Color.Tertiary, Tags = new() { "CI/CD" }, Progress = 100, CommentCount = 2, DueDate = "Apr 18" },
                new() { Id = "PRJ-033", Title = "Database schema migration for v2 entities", Priority = "Critical", Type = "Task", Assignee = "Lucas M.", AvatarColor = Color.Primary, Tags = new() { "DB", "EF Core" }, Progress = 100, HasAttachment = true, DueDate = "Apr 17" },
            }
        },
    };
    
    public static List<CalendarEvent> Events = new()
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
    
    public static List<CalendarCategoryFilter> Categories = new()
    {
        new() { Name = "Meeting",  Color = Color.Primary,   Hex = "#6366F1" },
        new() { Name = "Review",   Color = Color.Warning,   Hex = "#F59E0B" },
        new() { Name = "Release",  Color = Color.Success,   Hex = "#10B981" },
        new() { Name = "Personal", Color = Color.Secondary, Hex = "#8B5CF6" },
        new() { Name = "Deadline", Color = Color.Error,     Hex = "#EF4444" },
    };
}