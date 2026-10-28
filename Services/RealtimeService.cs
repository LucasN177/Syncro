using Syncro.Core.Interfaces.Infrastructure;
using Syncro.Core.Interfaces.Services;

namespace Syncro.Services;

public class RealtimeService(IRealtimeRepository realtimeRepository) : IRealtimeService
{
    // Ohne Filter: Supabase filtert Löschungen nicht nach Spalten, RLS begrenzt Inserts und Updates
    public Task<IAsyncDisposable?> SubscribeToIssues(Action onChange) =>
        realtimeRepository.Subscribe("issues", null, onChange);

    public Task<IAsyncDisposable?> SubscribeToNotifications(Guid userId, Action onChange) =>
        realtimeRepository.Subscribe("notifications", $"user_id=eq.{userId}", onChange);
}
