namespace Syncro.Core.Constants;

/// <summary>
/// Syncro liegt in einem eigenen Schema, damit es sich das Supabase-Projekt mit anderen Apps
/// (z. B. Monexa im Schema public) teilen kann. Siehe Database/000_preflight.sql.
/// </summary>
public static class SyncroDatabase
{
    /// <summary>Muss im Supabase-Dashboard unter Data API → Exposed schemas eingetragen sein.</summary>
    public const string Schema = "syncro";

    /// <summary>Privater Storage-Bucket für Issue-Anhänge (Database/008_attachments.sql).</summary>
    public const string AttachmentsBucket = "syncro-attachments";
}
