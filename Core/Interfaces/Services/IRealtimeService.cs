namespace Syncro.Core.Interfaces.Services;

/// <summary>Live-Updates über Supabase Realtime. Abos liefern null, wenn Realtime nicht verfügbar ist.</summary>
public interface IRealtimeService
{
    /// <summary>Änderungen an Issues aller sichtbaren Projekte (inkl. Löschungen).</summary>
    Task<IAsyncDisposable?> SubscribeToIssues(Action onChange);

    /// <summary>Neue oder geänderte Benachrichtigungen des Nutzers.</summary>
    Task<IAsyncDisposable?> SubscribeToNotifications(Guid userId, Action onChange);
}
