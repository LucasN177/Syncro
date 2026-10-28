namespace Syncro.Core.Constants;

/// <summary>Rollen entsprechen dem Check-Constraint auf project_members.role.</summary>
public static class ProjectRoles
{
    public const string Admin = "admin";
    public const string Member = "member";
    public const string Viewer = "viewer";

    public static readonly IReadOnlyList<string> All = [Admin, Member, Viewer];

    public static bool CanEdit(string? role) => role is Admin or Member;

    public static string DisplayName(string role) => role switch
    {
        Admin => "Admin",
        Viewer => "Betrachter",
        _ => "Mitglied"
    };
}
