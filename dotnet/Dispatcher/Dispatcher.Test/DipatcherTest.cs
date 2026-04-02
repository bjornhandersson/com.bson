using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using bson.Dispatcher.Hash;
using FluentAssertions;

namespace bson.Dispatcher.Test
{
    public class AsyncDispatcherTest
    {
        [TestCase(PartitionKeyAlgorithm.Murmur2, 0.06)]
        [TestCase(PartitionKeyAlgorithm.FNV1a, 0.000001)]
        public void Hashes_Should_distribute_evenly(
            PartitionKeyAlgorithm keyAlgorithm,
            decimal tolerancePct
        )
        {
            IHashGenerator hashGenerator = keyAlgorithm switch
            {
                PartitionKeyAlgorithm.FNV1a => new FNV1a(),
                PartitionKeyAlgorithm.Murmur2 => new MurmurHash2(),
                _ => throw new ArgumentOutOfRangeException(
                    nameof(keyAlgorithm),
                    keyAlgorithm,
                    null
                ),
            };

            var result = new int[2];
            for (int i = 0; i < 10_000; i++)
            {
                uint hash = hashGenerator.Hash(BitConverter.GetBytes(i));
                result[hash % 2]++;
            }

            decimal diff = (decimal)Math.Abs(result[0] - result[1]);
            decimal pctOff = diff / 1000;

            // Tolerance in full percentage
            Assert.Less(pctOff, tolerancePct);
        }

        [Test]
        public async Task Dispatcher_Should_apply_back_pressure_when_capacity_is_reached()
        {
            // Arrange
            int partitions = 2;
            int taskCompleted = 0;
            var tasksStarted = new TaskCompletionSource<bool>();
            var completed = new TaskCompletionSource<bool>();
            var enqueueBlocked = new TaskCompletionSource<bool>();

            using IAsyncDispatcher dispatcher = new AsyncDispatcher(
                new DispatcherOptions { Partitions = partitions, MaxCapacity = 1 }
            );

            // Act - Enqueue tasks to fill both partitions to capacity
            var enqueueTasks = new List<Task>();
            for (int i = 0; i < 4; i++)
            {
                int partition = i % partitions;
                var enqueueTask = dispatcher
                    .EnqueueAsync(
                        partition,
                        async (_) =>
                        {
                            // Signal that this task has started execution
                            if (Interlocked.Increment(ref taskCompleted) == 2)
                            {
                                // First 2 tasks (one per partition) have started
                                tasksStarted.SetResult(true);
                            }
                            await completed.Task;
                        }
                    )
                    .AsTask();
                enqueueTasks.Add(enqueueTask);
            }

            // Wait for the first 2 tasks to start (one per partition)
            // This ensures both partitions are occupied and the remaining 2 tasks are queued
            await tasksStarted.Task;

            // Start the 5th enqueue operation on a background task
            // This should block due to back pressure
            var blockedEnqueueTask = Task.Run(async () =>
            {
                try
                {
                    await dispatcher.EnqueueAsync(
                        partition: 0,
                        async (_) =>
                        {
                            Interlocked.Increment(ref taskCompleted);
                            await completed.Task;
                        }
                    );
                    return true; // Enqueue succeeded
                }
                catch
                {
                    return false; // Enqueue failed
                }
            });

            // Signal that we've started the blocked enqueue attempt
            _ = Task.Run(async () =>
            {
                await Task.Yield(); // Let the enqueue attempt start
                enqueueBlocked.SetResult(true);
            });

            await enqueueBlocked.Task;

            // Assert - The enqueue should be blocked (not completed)
            blockedEnqueueTask
                .IsCompleted.Should()
                .BeFalse(because: "Task should be blocked by back pressure");

            // Verify that only 2 tasks have started execution (the others are queued)
            taskCompleted
                .Should()
                .Be(2, because: "Only 2 tasks should have started (one per partition)");

            // Free up the dispatcher by completing all waiting tasks
            completed.SetResult(true);

            // Wait for all enqueue operations to complete
            await Task.WhenAll(enqueueTasks);
            var enqueueResult = await blockedEnqueueTask;

            // Assert the blocked enqueue eventually succeeded
            enqueueResult
                .Should()
                .BeTrue(because: "Enqueue should succeed after capacity is freed");

            // Verify all tasks eventually completed
            taskCompleted
                .Should()
                .Be(5, because: "All 5 tasks should complete after releasing the block");
        }

        [Test]
        [Category("DispatcherPerformance")]
        [Ignore("For performance testing only")]
        public async Task Dispatcher_Should_handle_load_over_time()
        {
            int partitions = Environment.ProcessorCount;
            int taskCompleted = 0;
            int iterations = 1_000_000;
            var completed = new TaskCompletionSource<bool>();
            using IAsyncDispatcher dispatcher = new AsyncDispatcher(
                new DispatcherOptions { Partitions = partitions, MaxCapacity = 2000 }
            );

            var stopwatch = new Stopwatch();
            stopwatch.Start();
            for (int i = 0; i < iterations; i++)
            {
                int partition = i % partitions;
                await dispatcher.EnqueueAsync(
                    partition,
                    (_) =>
                    {
                        Thread.SpinWait(50_000); // Around 1 ms
                        if (Interlocked.Increment(ref taskCompleted) == iterations)
                        {
                            completed.SetResult(true);
                        }

                        return default;
                    }
                );
            }

            var completedTask = await Task.WhenAny(completed.Task, Task.Delay(30_000));
            if (completedTask != completed.Task)
            {
                Console.WriteLine($"Timed out. Task completed: {taskCompleted}");
            }

            stopwatch.Stop();
            Console.WriteLine(
                $"Elapsed time: {stopwatch.Elapsed.TotalMilliseconds} ms Completed tasks: {taskCompleted} CPU: {partitions}"
            );
        }

        [Test]
        public async Task Dispatcher_Should_drain_queued_tasks_when_DrainOnDispose_is_true()
        {
            // Arrange
            int completed = 0;
            var allEnqueued = new TaskCompletionSource<bool>();

            var dispatcher = new AsyncDispatcher(
                new DispatcherOptions
                {
                    Partitions = 1,
                    MaxCapacity = 100,
                    DrainOnDispose = true,
                }
            );

            // Block the worker so items queue up behind it
            var gate = new TaskCompletionSource<bool>();
            await dispatcher.EnqueueAsync(
                partition: 0,
                async (_) =>
                {
                    await gate.Task;
                    Interlocked.Increment(ref completed);
                }
            );

            // Enqueue 5 more items while the worker is blocked
            for (int i = 0; i < 5; i++)
            {
                await dispatcher.EnqueueAsync(
                    partition: 0,
                    (_) =>
                    {
                        Interlocked.Increment(ref completed);
                        return default;
                    }
                );
            }

            // Release the gate so items can start processing
            gate.SetResult(true);

            // Dispose with drain — should wait for all 6 items to finish
            await dispatcher.DisposeAsync();

            completed
                .Should()
                .Be(6, because: "all queued tasks should be processed before shutdown");
        }

        [Test]
        public async Task Dispatcher_Should_discard_queued_tasks_when_DrainOnDispose_is_false()
        {
            // Arrange
            int completed = 0;

            var dispatcher = new AsyncDispatcher(
                new DispatcherOptions
                {
                    Partitions = 1,
                    MaxCapacity = 100,
                    DrainOnDispose = false,
                }
            );

            // Block the worker so items queue up behind it
            var gate = new TaskCompletionSource<bool>();
            await dispatcher.EnqueueAsync(
                partition: 0,
                async (ct) =>
                {
                    await Task.Delay(Timeout.Infinite, ct);
                    Interlocked.Increment(ref completed);
                }
            );

            // Enqueue 5 more items while the worker is blocked
            for (int i = 0; i < 5; i++)
            {
                await dispatcher.EnqueueAsync(
                    partition: 0,
                    (_) =>
                    {
                        Interlocked.Increment(ref completed);
                        return default;
                    }
                );
            }

            // Dispose without drain — should cancel immediately
            await dispatcher.DisposeAsync();

            completed
                .Should()
                .Be(0, because: "queued tasks should be discarded and the blocked task cancelled");
        }

        [Test]
        public async Task Dispatcher_Should_abort_long_running_tasks()
        {
            // Arrange
            using IAsyncDispatcher dispatcher = new AsyncDispatcher(
                new DispatcherOptions
                {
                    TaskTimeout = TimeSpan.FromMilliseconds(500),
                    Partitions = 2,
                }
            );

            // Act
            var stopwatch = Stopwatch.StartNew();
            await dispatcher.EnqueueAsync(
                partition: 0,
                async (cancellationToken) =>
                {
                    await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
                }
            );
            stopwatch.Stop();

            // Assert
            Assert.That(stopwatch.Elapsed, Is.LessThan(TimeSpan.FromSeconds(2)));

            // Can queue more work after task being cancelled
            var didExecute = new TaskCompletionSource<bool>();
            await dispatcher.EnqueueAsync(
                partition: 0,
                (cancellationToken) =>
                {
                    didExecute.SetResult(true);
                    return default;
                }
            );

            var completedTask = await Task.WhenAny(
                didExecute.Task,
                Task.Delay(TimeSpan.FromSeconds(10))
            );
            Assert.That(completedTask, Is.SameAs(didExecute.Task));
        }
    }
}
