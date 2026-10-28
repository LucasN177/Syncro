using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
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
    [Inject] private IJSRuntime JS { get; set; } = default!;

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
    private readonly List<AttachmentDto> _attachments = [];
    private bool _isUploading;

    // Kommentare und Anhänge werden sofort gespeichert; das Board muss danach neu laden
    private bool _hasChanges;

    /// <summary>Zeigt, wie Mitglieder per @Benutzername erwähnt werden.</summary>
    private string MentionHint
    {
        get
        {
            var example = Members.FirstOrDefault(member =>
                member.UserId != AuthService.CurrentUserId && !string.IsNullOrWhiteSpace(member.Profile?.Username));
            return example is null
                ? "Mit @benutzername erwähnst du Projektmitglieder."
                : $"Mit @{example.Profile!.Username} erwähnst du {example.DisplayName}.";
        }
    }

    protected override async Task OnInitializedAsync()
    {
        _tagsText = string.Join(", ", Issue.Tags);
        if (IsNew)
            return;

        _isLoadingComments = true;
        var commentsTask = IssueService.GetComments(Issue.Id);
        var attachmentsTask = IssueService.GetAttachments(Issue.Id);
        await Task.WhenAll(commentsTask, attachmentsTask);

        if (commentsTask.Result is { IsSuccess: true, Data: not null })
            _comments.AddRange(commentsTask.Result.Data);
        if (attachmentsTask.Result is { IsSuccess: true, Data: not null })
            _attachments.AddRange(attachmentsTask.Result.Data);
        _isLoadingComments = false;
    }

    // ── Anhänge ──────────────────────────────────────────────────────────────

    private async Task UploadAttachmentAsync(IBrowserFile? file)
    {
        if (file is null)
            return;

        _errorMessage = null;
        if (file.Size > Services.IssueService.MaxAttachmentBytes)
        {
            _errorMessage = "Dateien dürfen höchstens 10 MB groß sein.";
            return;
        }

        _isUploading = true;
        StateHasChanged();
        try
        {
            await using var stream = file.OpenReadStream(Services.IssueService.MaxAttachmentBytes);
            using var memory = new MemoryStream();
            await stream.CopyToAsync(memory);

            var result = await IssueService.AddAttachment(Issue, file.Name, file.ContentType, memory.ToArray());
            if (result is { IsSuccess: true, Data: not null })
            {
                _attachments.Add(result.Data);
                _hasChanges = true;
            }
            else
            {
                _errorMessage = result.ErrorMessage ?? "Datei konnte nicht hochgeladen werden.";
            }
        }
        catch (IOException exception)
        {
            _errorMessage = $"Datei konnte nicht gelesen werden: {exception.Message}";
        }
        finally
        {
            _isUploading = false;
        }
    }

    private async Task OpenAttachmentAsync(AttachmentDto attachment)
    {
        var result = await IssueService.GetAttachmentUrl(attachment);
        if (result is { IsSuccess: true, Data: not null })
            await JS.InvokeVoidAsync("open", result.Data, "_blank", "noopener");
        else
            _errorMessage = result.ErrorMessage ?? "Datei konnte nicht geöffnet werden.";
    }

    private async Task DeleteAttachmentAsync(AttachmentDto attachment)
    {
        var confirmed = await DialogService.ShowMessageBoxAsync(
            "Anhang löschen?", $"„{attachment.FileName}“ wird endgültig gelöscht.",
            yesText: "Löschen", cancelText: "Abbrechen");
        if (confirmed != true)
            return;

        var result = await IssueService.DeleteAttachment(attachment);
        if (!result.IsSuccess)
        {
            _errorMessage = result.ErrorMessage ?? "Anhang konnte nicht gelöscht werden.";
            return;
        }

        _attachments.Remove(attachment);
        _hasChanges = true;
    }

    private static string AttachmentIcon(AttachmentDto attachment) => attachment.ContentType switch
    {
        { } type when type.StartsWith("image/") => Icons.Material.Outlined.Image,
        "application/pdf" => Icons.Material.Outlined.PictureAsPdf,
        { } type when type.StartsWith("text/") => Icons.Material.Outlined.Description,
        _ => Icons.Material.Outlined.InsertDriveFile
    };

    private static string FormatSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:0.#} KB",
        _ => $"{bytes / (1024.0 * 1024):0.#} MB"
    };

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
