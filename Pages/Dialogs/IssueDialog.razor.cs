using Microsoft.AspNetCore.Components;
using MudBlazor;
using Syncro.Core.Interfaces.Services;
using Syncro.Core.Models;
using Syncro.Core.Models.Database;

namespace Syncro.Pages.Dialogs;

public partial class IssueDialog : ComponentBase
{
    [CascadingParameter] public required IMudDialogInstance MudDialog { get; set; }
    [Inject] private IIssueService IssueService { get; set; } = default!;
    [Inject] private IAuthService AuthService { get; set; } = default!;
    [Inject] private IDialogService DialogService { get; set; } = default!;

    /// <summary>Bearbeitungskopie; Id leer für ein neues Issue.</summary>
    [Parameter] public IssueDto Issue { get; set; } = new();

    [Parameter] public string? ProjectKey { get; set; }

    [Parameter] public IReadOnlyList<ProjectMember> Members { get; set; } = [];

    /// <summary>Admins und Mitglieder dürfen bearbeiten, Betrachter nur lesen.</summary>
    [Parameter] public bool CanEdit { get; set; }

    [Parameter] public bool IsAdmin { get; set; }

    private bool IsNew => Issue.Id == Guid.Empty;
    private readonly List<IssueCommentDto> _comments = [];
    private string _tagsText = string.Empty;
    private string _newComment = string.Empty;
    private string? _errorMessage;
    private bool _isBusy;
    private bool _isLoadingComments;

    // Kommentare werden sofort gespeichert; das Board muss danach neu laden
    private bool _hasChanges;

    protected override async Task OnInitializedAsync()
    {
        _tagsText = string.Join(", ", Issue.Tags);
        if (IsNew)
            return;

        _isLoadingComments = true;
        var result = await IssueService.GetComments(Issue.Id);
        if (result is { IsSuccess: true, Data: not null })
            _comments.AddRange(result.Data);
        _isLoadingComments = false;
    }

    private ProjectMember? MemberFor(Guid? userId) => Members.FirstOrDefault(member => member.UserId == userId);

    private string AssigneeName(Guid? userId) =>
        userId is null ? "Nicht zugewiesen" : MemberFor(userId)?.DisplayName ?? "Ehemaliges Mitglied";

    private bool CanDeleteComment(IssueCommentDto comment) =>
        IsAdmin || (comment.UserId is { } authorId && authorId == AuthService.CurrentUserId);

    private async Task SaveAsync()
    {
        _errorMessage = null;
        Issue.Tags = _tagsText.Split(',').ToList();

        _isBusy = true;
        var result = await IssueService.SaveIssue(Issue);
        _isBusy = false;

        if (!result.IsSuccess || result.Data is null)
        {
            _errorMessage = result.ErrorMessage ?? "Issue konnte nicht gespeichert werden.";
            return;
        }

        MudDialog.Close(DialogResult.Ok(true));
    }

    private async Task DeleteAsync()
    {
        var confirmed = await DialogService.ShowMessageBoxAsync(
            "Issue löschen?",
            $"„{Issue.Title}“ wird mit allen Kommentaren endgültig gelöscht.",
            yesText: "Löschen", cancelText: "Abbrechen");

        if (confirmed != true)
            return;

        _isBusy = true;
        var result = await IssueService.DeleteIssue(Issue.Id);
        _isBusy = false;

        if (!result.IsSuccess)
        {
            _errorMessage = result.ErrorMessage ?? "Issue konnte nicht gelöscht werden.";
            return;
        }

        MudDialog.Close(DialogResult.Ok(true));
    }

    private async Task AddCommentAsync()
    {
        _errorMessage = null;
        _isBusy = true;
        var result = await IssueService.AddComment(Issue.Id, _newComment);
        _isBusy = false;

        if (!result.IsSuccess || result.Data is null)
        {
            _errorMessage = result.ErrorMessage ?? "Kommentar konnte nicht gespeichert werden.";
            return;
        }

        _comments.Add(result.Data);
        _newComment = string.Empty;
        _hasChanges = true;
    }

    private async Task DeleteCommentAsync(IssueCommentDto comment)
    {
        var result = await IssueService.DeleteComment(comment.Id);
        if (!result.IsSuccess)
        {
            _errorMessage = result.ErrorMessage ?? "Kommentar konnte nicht gelöscht werden.";
            return;
        }

        _comments.Remove(comment);
        _hasChanges = true;
    }

    private void Close()
    {
        if (_hasChanges)
            MudDialog.Close(DialogResult.Ok(true));
        else
            MudDialog.Cancel();
    }
}
