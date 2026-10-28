using Syncro.Core.Interfaces.Response;
using Syncro.Core.Models.Database;

namespace Syncro.Core.Interfaces.Infrastructure;

public interface IAttachmentRepository
{
    Task<IResponse<IReadOnlyList<AttachmentDto>>> GetAttachments(Guid issueId);

    /// <summary>Lädt die Datei in den Bucket und legt den Eintrag an; bei Fehlern wird die Datei wieder entfernt.</summary>
    Task<IResponse<AttachmentDto>> Upload(Guid projectId, Guid issueId, string fileName, string? contentType, byte[] content);

    /// <summary>Zeitlich begrenzter Download-Link für den privaten Bucket.</summary>
    Task<IResponse<string>> CreateDownloadUrl(string storagePath, int expiresInSeconds);

    Task<IResponse> Delete(AttachmentDto attachment);
}
