using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Hotsapi.Uploader.Common
{
    internal static class WorkerPool
    {
        private static TaskScheduler BackgroundScheduler = new LowPriorityScheduler();
        public static Task RunBackground(Action action) => RunBackground(action, CancellationToken.None);
        public static Task RunBackground(Action action, CancellationToken token) => Task.Factory.StartNew(action, token, TaskCreationOptions.LongRunning, BackgroundScheduler);
        public static Task<A> RunBackground<A>(Func<A> action) => RunBackground(action, CancellationToken.None);
        public static Task<A> RunBackground<A>(Func<A> action, CancellationToken token) => Task.Factory.StartNew(action, token, TaskCreationOptions.LongRunning, BackgroundScheduler);

    }

    /// <summary>
    /// Fixed number threadpool task scheduler that runs tasks on low-priority threads
    /// </summary>
    public class LowPriorityScheduler : TaskScheduler, IDisposable
    {
        private BlockingCollection<Task> TaskQueue { get; } = new BlockingCollection<Task>();
        private IEnumerable<Task> Consumable => TaskQueue.GetConsumingEnumerable();

        public LowPriorityScheduler(int maxThreads)
        {
            for (var i = 0; i < maxThreads; i++) {
                var workerThread = new Thread(PerformTasks) {
                    Name = $"Low Priority Tread {i}/{maxThreads}",
                    IsBackground = true,
                    Priority = ThreadPriority.BelowNormal
                };
                workerThread.Start();
            }
        }

        public LowPriorityScheduler() : this(Environment.ProcessorCount) { }

        private void PerformTasks()
        {
            foreach (var task in Consumable) {
                if (task.Status == TaskStatus.WaitingToRun || task.Status == TaskStatus.WaitingForActivation || task.Status == TaskStatus.Created) {
                    // execute the task under the current thread
                    if (!base.TryExecuteTask(task)) Trace.WriteLine("Error");
                } else {
                    Trace.WriteLine("Unrunnable Task Status");
                }
            }
            var col = TaskQueue;
            if (col != null) col.Dispose();
        }
        protected override IEnumerable<Task> GetScheduledTasks() => TaskQueue;
        protected override void QueueTask(Task task) => TaskQueue.Add(task);
        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued) => false;

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue) {
                if (disposing) {
                    TaskQueue.CompleteAdding();
                }
                disposedValue = true;
            }
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose() =>
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
        #endregion
    }
}
