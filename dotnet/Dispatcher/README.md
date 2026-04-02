# Dispatcher

Parallel task processing with strict ordering per partition key. Same key = sequential. Different keys = parallel.

## Install

```
dotnet add package Bson.AsyncDispatcher
```

## Usage

```csharp
await using var dispatcher = new AsyncDispatcher();

await dispatcher.EnqueueAsync("order-123", async ct =>
{
    await HandleOrder(ct);
});
```

Same key = sequential. Different keys = parallel.

## Configuration

```csharp
var dispatcher = new AsyncDispatcher(new DispatcherOptions
{
    Partitions = 8,                              // default: Environment.ProcessorCount
    MaxCapacity = 1000,                          // backpressure per partition
    TaskTimeout = TimeSpan.FromSeconds(30),      // per-task cancellation
    DrainOnDispose = true,                       // finish queued work on shutdown
    ExceptionHandler = ex => Log.Error(ex),      // handle task failures
    PartitionKeyAlgorithm = PartitionKeyAlgorithm.Murmur2
});
```

## How it works

N partitions, each with a dedicated worker and a `Channel<T>` queue. Partition keys are hashed and mapped via modulo. Multiple keys may share a partition — that's safe, just stricter ordering.

```
Enqueue("A", work) ──► Partition 0 ── [task1] → [task2] → worker
Enqueue("B", work) ──► Partition 1 ── [task1] → worker
Enqueue("C", work) ──► Partition 0 ── [task3] → (queued behind A)
```
