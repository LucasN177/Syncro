using System.Text.RegularExpressions;
using Syncro.Core.Constants;
using Syncro.Core.Interfaces.Infrastructure;
using Syncro.Core.Interfaces.Response;
using Syncro.Core.Models;
using Syncro.Core.Models.Database;
using static Supabase.Postgrest.Constants;

namespace Syncro.Infrastructure;

public class AttachmentRepository(Supabase.Client client) : IAttachmentRepository
{
    private const string Bucket = SyncroDatabase.AttachmentsBucket;

    public async Task<IResponse<IReadOnlyList<AttachmentDto>>> GetAttachments(Guid issueId)
    {
        try
        {
            var result = await client.From<AttachmentDto>()
                .Where(attachment => attachment.IssueId == issueId)
                .Order(attachment => attachment.CreatedAt, Ordering.Ascending)
                .Get();

            return Response<IReadOnlyList<AttachmentDto>>.Success(result.Models);
        }
        catch (Exception exception)
        {
            return Response<IReadOnlyList<AttachmentDto>>.Failure(exception.Message);
        }
    }

    public async Task<IResponse<AttachmentDto>> Upload(Guid projectId, Guid issueId, string fileName, string? contentType, byte[] content)
    {
        // Eindeutiger, URL-sicherer Pfad; der Originalname bleibt in file_name erhalten
        var safeName = Regex.Replace(fileName, "[^A-Za-z0-9._-]", "_");
        var path = $"{projectId}/{issueId}/{Guid.NewGuid():N}-{safeName}";

        try
        {
            await client.Storage.From(Bucket).Upload(content, path, new Supabase.Storage.FileOptions
            {
                ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType,
                Upsert = false
            });
        }
        catch (Exception exception)
        {
            return Response<AttachmentDto>.Failure($"Upload fehlgeschlagen: {exception.Message}");
        }

        try
        {
            var result = await client.From<AttachmentDto>().Insert(new AttachmentDto
            {
                IssueId = issueId,
                ProjectId = projectId,
                FileName = fileName,
                StoragePath = path,
                ContentType = contentType,
                SizeBytes = content.LongLength
            });

            var attachment = result.Models.FirstOrDefault();
            if (attachment is not null)
                return Response<AttachmentDto>.Success(attachment);

            await RemoveFile(path);
            return Response<AttachmentDto>.Failure("Anhang konnte nicht gespeichert werden.");
        }
        catch (Exception exception)
        {
            await RemoveFile(path);
            return Response<AttachmentDto>.Failure(exception.Message);
        }
    }

    public async Task<IResponse<string>> CreateDownloadUrl(string storagePath, int expiresInSeconds)
    {
        try
        {
            var url = await client.Storage.From(Bucket).CreateSignedUrl(storagePath, expiresInSeconds);
            return string.IsNullOrWhiteSpace(url)
                ? Response<string>.Failure("Download-Link konnte nicht erstellt werden.")
                : Response<string>.Success(url);
        }
        catch (Exception exception)
        {
            return Response<string>.Failure(exception.Message);
        }
    }

    public async Task<IResponse> Delete(AttachmentDto attachment)
    {
        try
        {
            await client.Storage.From(Bucket).Remove(attachment.StoragePath);
            await client.From<AttachmentDto>()
                .Where(existing => existing.Id == attachment.Id)
                .Delete();

            return Response.Success();
        }
        catch (Exception exception)
        {
            return Response.Failure(exception.Message, exception);
        }
    }

    private async Task RemoveFile(string path)
    {
        try
        {
            await client.Storage.From(Bucket).Remove(path);
        }
        catch (Exception)
        {
            // Aufräumen ist best effort
        }
    }
}
