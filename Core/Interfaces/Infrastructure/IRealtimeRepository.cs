namespace Syncro.Core.Interfaces.Infrastructure;

public interface IRealtimeRepository
{
    /// <summary>
    /// Meldet Änderungen an einer Tabelle (Insert, Update, Delete). Liefert null, wenn Realtime nicht
    /// verfügbar ist; die App funktioniert dann ohne Live-Updates weiter.
    /// </summary>
    /// <param name="table">Tabelle im Schema syncro</param>
    /// <param name="filter">Optionaler Realtime-Filter, z. B. user_id=eq.{id}</param>
    /// <param name="onChange">Wird bei jeder Änderung aufgerufen</param>
    Task<IAsyncDisposable?> Subscribe(string table, string? filter, Action onChange);
}
