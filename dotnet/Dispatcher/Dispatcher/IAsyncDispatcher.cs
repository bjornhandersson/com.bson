using System;
using System.Threading;
using System.Threading.Tasks;

namespace Bson.Dispatcher
{
    public interface IAsyncDispatcher : IDisposable, IAsyncDisposable
    {
        ValueTask EnqueueAsync(int partition, Func<CancellationToken, ValueTask> action);

        ValueTask EnqueueAsync(byte[] partitionKey, Func<CancellationToken, ValueTask> action);

        ValueTask EnqueueAsync(string partitionKey, Func<CancellationToken, ValueTask> action);

        Task StopAsync();
    }
}
