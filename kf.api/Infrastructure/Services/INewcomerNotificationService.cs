using System.Runtime.CompilerServices;
using System.Threading.Channels;
using KnightFight.Api.Models.Responses;

namespace KnightFight.Api.Infrastructure.Services;

public interface INewcomerNotificationService
{
    ValueTask PublishAsync(ChallengerNewcomerEvent newcomer, CancellationToken cancellationToken = default);
    IAsyncEnumerable<ChallengerNewcomerEvent> SubscribeAsync(CancellationToken cancellationToken);
}

public class NewcomerNotificationService : INewcomerNotificationService
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, Channel<ChallengerNewcomerEvent>> _subscribers = new();

    public ValueTask PublishAsync(ChallengerNewcomerEvent newcomer, CancellationToken cancellationToken = default)
    {
        foreach (var channel in _subscribers.Values)
        {
            channel.Writer.TryWrite(newcomer);
        }

        return ValueTask.CompletedTask;
    }

    public async IAsyncEnumerable<ChallengerNewcomerEvent> SubscribeAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var id = Guid.NewGuid();
        var channel = Channel.CreateBounded<ChallengerNewcomerEvent>(new BoundedChannelOptions(100)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });

        _subscribers.TryAdd(id, channel);

        try
        {
            while (await channel.Reader.WaitToReadAsync(cancellationToken))
            {
                while (channel.Reader.TryRead(out var item))
                {
                    yield return item;
                }
            }
        }
        finally
        {
            _subscribers.TryRemove(id, out _);
            channel.Writer.TryComplete();
        }
    }
}
