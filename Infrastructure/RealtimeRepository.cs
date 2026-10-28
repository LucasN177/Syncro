using Microsoft.Extensions.Logging;
using Supabase.Realtime;
using Supabase.Realtime.PostgresChanges;
using Syncro.Core.Constants;
using Syncro.Core.Interfaces.Infrastructure;
using static Supabase.Realtime.PostgresChanges.PostgresChangesOptions;

namespace Syncro.Infrastructure;

public class RealtimeRepository(Supabase.Client client, ILogger<RealtimeRepository> logger) : IRealtimeRepository
{
    public async Task<IAsyncDisposable?> Subscribe(string table, string? filter, Action onChange)
    {
        try
        {
            // Realtime prüft RLS mit dem Token des Nutzers
            if (client.Auth.CurrentSession?.AccessToken is { } accessToken)
                client.Realtime.SetAuth(accessToken);

            if (client.Realtime.Socket is not { IsConnected: true })
                await client.Realtime.ConnectAsync();

            var channel = client.Realtime.Channel($"syncro:{table}:{Guid.NewGuid():N}");
            channel.Register(new PostgresChangesOptions(SyncroDatabase.Schema, table, ListenType.All, filter));
            channel.AddPostgresChangeHandler(ListenType.All, (_, _) => onChange());
            await channel.Subscribe();

            return new ChannelSubscription(client, channel);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Realtime-Abo für {Table} nicht möglich", table);
            return null;
        }
    }

    private sealed class ChannelSubscription(Supabase.Client client, RealtimeChannel channel) : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            try
            {
                channel.Unsubscribe();
                client.Realtime.Remove(channel);
            }
            catch (Exception)
            {
                // Verbindung ist evtl. schon weg
            }
            return ValueTask.CompletedTask;
        }
    }
}
