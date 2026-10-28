using Microsoft.AspNetCore.Components;
using MudBlazor;
using Syncro.Core.Constants;
using Syncro.Core.Interfaces.Services;
using Syncro.Core.Mapping;
using Syncro.Core.Models;
using Syncro.Core.Models.Database;
using Syncro.Core.Models.Enums;
using Syncro.Services;

namespace Syncro.Pages;

public partial class Team : ComponentBase, IDisposable
{
    [Inject] private IProjectService ProjectService { get; set; } = default!;
    [Inject] private IProjectMemberService MemberService { get; set; } = default!;
    [Inject] private IIssueService IssueService { get; set; } = default!;
    [Inject] private IAuthService AuthService { get; set; } = default!;
    [Inject] private AppStateService AppState { get; set; } = default!;
    [Inject] private IDialogService DialogService { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;

    private IReadOnlyList<ProjectDto> _projects = [];
    private IReadOnlyList<ProjectMember> _members = [];
    private IReadOnlyList<IssueDto> _issues = [];
    private ProjectDto? _project;
    private bool _isAdmin;
    private bool _isLoading = true;

    private string _inviteEmail = string.Empty;
    private string _inviteRole = ProjectRoles.Member;
    private bool _isInviting;

    protected override async Task OnInitializedAsync()
    {
        AuthService.OnAuthStateChanged += HandleAuthStateChanged;
        await LoadProjects();
    }

    public void Dispose() => AuthService.OnAuthStateChanged -= HandleAuthStateChanged;

    private void HandleAuthStateChanged() => InvokeAsync(async () =>
    {
        await LoadProjects();
        StateHasChanged();
    });

    private async Task LoadProjects()
    {
        _project = null;
        if (!AuthService.IsAuthenticated)
        {
            _isLoading = false;
            return;
        }

        _isLoading = true;
        var result = await ProjectService.GetProjects();
        _projects = result.Data ?? [];
        _project = _projects.FirstOrDefault(project => project.Id == AppState.CurrentProjectId)
                   ?? _projects.FirstOrDefault(project => project.Status == "Aktiv")
                   ?? _projects.FirstOrDefault();

        await LoadMembers();
        _isLoading = false;
    }

    private async Task LoadMembers()
    {
        if (_project is null)
            return;

        var membersResult = await MemberService.GetMembers(_project.Id);
        _members = membersResult.Data ?? [];
        if (!membersResult.IsSuccess)
            Snackbar.Add(membersResult.ErrorMessage ?? "Mitglieder konnten nicht geladen werden.", Severity.Error);

        var issuesResult = await IssueService.GetIssues(_project.Id);
        _issues = issuesResult.Data ?? [];

        _isAdmin = _project.UserId == AuthService.CurrentUser?.Id
                   || _members.FirstOrDefault(member => member.UserId == AuthService.CurrentUserId)?.Role == ProjectRoles.Admin;
    }

    private async Task SelectProject(Guid projectId)
    {
        _project = _projects.FirstOrDefault(project => project.Id == projectId);
        AppState.SetCurrentProject(_project?.Id);
        _isLoading = true;
        await LoadMembers();
        _isLoading = false;
    }

    private int OpenIssuesFor(Guid userId) =>
        _issues.Count(issue => issue.AssigneeId == userId && issue.Status.ToColumn() != Column.Done);

    private async Task Invite()
    {
        if (_project is null)
            return;

        _isInviting = true;
        var result = await MemberService.InviteByEmail(_project.Id, _inviteEmail, _inviteRole);
        _isInviting = false;

        if (!result.IsSuccess)
        {
            Snackbar.Add(result.ErrorMessage ?? "Einladung fehlgeschlagen.", Severity.Error);
            return;
        }

        Snackbar.Add($"{result.Data?.DisplayName} wurde hinzugefügt.", Severity.Success);
        _inviteEmail = string.Empty;
        await LoadMembers();
    }

    private async Task ChangeRole(ProjectMember member, string role)
    {
        var result = await MemberService.UpdateRole(member.MemberId, role);
        if (!result.IsSuccess)
        {
            Snackbar.Add(result.ErrorMessage ?? "Rolle konnte nicht geändert werden.", Severity.Error);
            return;
        }

        Snackbar.Add($"{member.DisplayName} ist jetzt {ProjectRoles.DisplayName(role)}.", Severity.Success);
        await LoadMembers();
    }

    private async Task RemoveMember(ProjectMember member)
    {
        var confirmed = await DialogService.ShowMessageBoxAsync(
            "Mitglied entfernen?",
            $"{member.DisplayName} verliert den Zugriff auf „{_project?.Name}“. Zugewiesene Issues werden freigegeben.",
            yesText: "Entfernen", cancelText: "Abbrechen");
        if (confirmed != true)
            return;

        var result = await MemberService.RemoveMember(member.MemberId);
        if (!result.IsSuccess)
        {
            Snackbar.Add(result.ErrorMessage ?? "Mitglied konnte nicht entfernt werden.", Severity.Error);
            return;
        }

        Snackbar.Add($"{member.DisplayName} wurde entfernt.", Severity.Success);
        await LoadMembers();
    }

    private async Task LeaveProject(ProjectMember member)
    {
        var confirmed = await DialogService.ShowMessageBoxAsync(
            "Projekt verlassen?",
            $"Du verlierst den Zugriff auf „{_project?.Name}“. Ein Admin kann dich erneut einladen.",
            yesText: "Verlassen", cancelText: "Abbrechen");
        if (confirmed != true)
            return;

        var result = await MemberService.RemoveMember(member.MemberId);
        if (!result.IsSuccess)
        {
            Snackbar.Add(result.ErrorMessage ?? "Projekt konnte nicht verlassen werden.", Severity.Error);
            return;
        }

        Snackbar.Add("Du hast das Projekt verlassen.", Severity.Success);
        AppState.SetCurrentProject(null);
        await LoadProjects();
    }
}
