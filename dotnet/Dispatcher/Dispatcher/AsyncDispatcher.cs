using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Bson.Dispatcher.Hash;

namespace Bson.Dispatcher
{
    public class AsyncDispatcher : IAsyncDispatcher
    {
        private readonly uint _partitionCount;
        private readonly uint _maxCapacity;
        private readonly Channel<Func<CancellationToken, ValueTask>>[] _partitions;
        private readonly Task[] _workers;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly DispatcherOptions _options;
        private int _stopped;

        private readonly IHashGenerator _hashGenerator;

        public AsyncDispatcher(DispatcherOptions? options = null)
        {
            _options = options ??= new DispatcherOptions();
            _partitionCount = _options.Partitions < 1 ? 1 : (uint)_options.Partitions;
            _maxCapacity = _options.MaxCapacity < 1 ? 1 : (uint)_options.MaxCapacity;
            _partitions = new Channel<Func<CancellationToken, ValueTask>>[_partitionCount];
            _workers = new Task[_partitionCount];
            _cancellationTokenSource = new CancellationTokenSource();
            _hashGenerator = _options.PartitionKeyAlgorithm switch
            {
                PartitionKeyAlgorithm.FNV1a => new FNV1a(),
                PartitionKeyAlgorithm.Murmur2 => new MurmurHash2(),
                _ => throw new ArgumentOutOfRangeException(
                    nameof(_options.PartitionKeyAlgorithm),
                    $"Unsupported partition key algorithm: {_options.PartitionKeyAlgorithm}"
                ),
            };

            for (int i = 0; i < _partitionCount; i++)
            {
                int localIndex = i;
                _partitions[i] = Channel.CreateBounded<Func<CancellationToken, ValueTask>>(
                    (int)_maxCapacity
                );
                _workers[i] = Task.Run(() =>
                    ProcessPartition(localIndex, _cancellationTokenSource.Token)
                );
            }
        }

        public ValueTask EnqueueAsync(int partition, Func<CancellationToken, ValueTask> action)
        {
            return _partitions[partition].Writer.WriteAsync(action);
        }

        public ValueTask EnqueueAsync(
            byte[] partitionKey,
            Func<CancellationToken, ValueTask> action
        )
        {
            return _partitions[GetPartitionKey(partitionKey)].Writer.WriteAsync(action);
        }

        public ValueTask EnqueueAsync(
            string partitionKey,
            Func<CancellationToken, ValueTask> action
        )
        {
            return EnqueueAsync(System.Text.Encoding.UTF8.GetBytes(partitionKey), action);
        }

        public async Task StopAsync()
        {
            await DisposeAsync();
        }

        private async Task ProcessPartition(int partition, CancellationToken cancellationToken)
        {
            var channel = _partitions[partition];
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    Func<CancellationToken, ValueTask> workFunc;
                    if (!channel.Reader.TryRead(out workFunc!))
                    {
                        workFunc = await channel.Reader.ReadAsync(cancellationToken);
                    }

                    using var taskCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                        cancellationToken
                    );

                    if (_options.TaskTimeout != TimeSpan.MinValue)
                    {
                        taskCancellation.CancelAfter(_options.TaskTimeout);
                    }

                    await workFunc(taskCancellation.Token);
                }
                catch (ChannelClosedException)
                {
                    break;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (OperationCanceledException)
                {
                    continue;
                }
                catch (Exception ex)
                {
                    // Let third party handle / log the exception
                    _options.ExceptionHandler.Invoke(ex);
                }
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (!SignalStop())
            {
                return;
            }

            await Task.WhenAll(_workers);
            _cancellationTokenSource.Dispose();
        }

        public void Dispose()
        {
            if (!SignalStop())
            {
                return;
            }

            // Do not block on workers — use DisposeAsync to await graceful shutdown.
            // Blocking here risks deadlocks when work items capture the calling context.
            _cancellationTokenSource.Dispose();
        }

        private bool SignalStop()
        {
            if (Interlocked.Exchange(ref _stopped, 1) == 1)
            {
                return false;
            }

            if (_options.DrainOnDispose)
            {
                foreach (var partition in _partitions)
                {
                    partition.Writer.Complete();
                }
            }
            else
            {
                _cancellationTokenSource.Cancel();
            }

            return true;
        }

        private uint GetPartitionKey(uint partition)
        {
            return partition % _partitionCount;
        }

        private uint GetPartitionKey(byte[] partitionKey)
        {
            return _hashGenerator.Hash(partitionKey) % _partitionCount;
        }
    }
}
