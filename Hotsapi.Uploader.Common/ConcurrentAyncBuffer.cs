using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Hotsapi.Uploader.Common
{
    /// <summary>
    /// A thread-safe, concurrent buffer that has asynchronous operations for enqueuing and bulk dequeueing.
    /// </summary>
    public class ConcurrentAyncBuffer<A>
    {
        private Queue<A> Items { get; } = new Queue<A>();
        private Queue<TaskCompletionSource<IEnumerable<A>>> BulkRequests { get; } = new Queue<TaskCompletionSource<IEnumerable<A>>>();
        private SemaphoreSlim Mutex { get; } = new SemaphoreSlim(1);
        /// <summary>
        /// Enqueue a single item
        /// </summary>
        /// <param name="item">the item to enqueue</param>
        /// <returns>A task that will be completed when the item is enqueued</returns>
        public Task EnqueueAsync(A item) => EnqueueAsync(item, CancellationToken.None);
        /// <summary>
        /// Enqueue a single item
        /// </summary>
        /// <param name="item">the item to enqueue</param>
        /// <param name="token">a cancellation token to cancel trying to enqueue the item</param>
        /// <returns>A task that will be completed when the item is enqueued</returns>
        public Task EnqueueAsync(A item, CancellationToken token) => EnqueueManyAsync(new List<A>() { item }, token);
        /// <summary>
        /// Enqueue all items
        /// </summary>
        /// <param name="items">the items to enqueue</param>
        /// <param name="token">a cancellation token to cancel trying to enqueue items</param>
        /// <returns>A task that will be completed when the items are enqueued</returns>
        public Task EnqueueManyAsync(IEnumerable<A> items, CancellationToken token) => Mutex.Locked(() => {
            var succeeded = false;
            while (!succeeded) {
                if (BulkRequests.Any()) {
                    succeeded = BulkRequests.Dequeue().TrySetResult(items);
                } else {
                    foreach (var item in items) {
                        Items.Enqueue(item);
                    }
                    succeeded = true;
                }
            }
        }, token);

        /// <summary>
        /// Enqueue all items
        /// </summary>
        /// <param name="items">the items to enqueue</param>
        /// <returns>a task that will be completed when the items are enqueued.</returns>
        public Task EnqueueManyAsync(IEnumerable<A> items) => EnqueueManyAsync(items, CancellationToken.None);

        /// <summary>
        /// Dequeue all A's as soon as they're available.
        /// </summary>
        /// <param name="token">A cancellationtoken that will cancel fetching data</param>
        /// <returns>A task that will complete with all A's available</returns>
        public async Task<IEnumerable<A>> DequeueAsync(CancellationToken token)
        {
            var locked = 0;
            try {
                await Mutex.WaitAsync(token);
                locked = 1;
                Task<IEnumerable<A>> resultTask;
                if (Items.Any()) {
                    var result = new List<A>();
                    while (Items.Any()) {
                        result.Add(Items.Dequeue());
                    }
                    resultTask = Task.FromResult<IEnumerable<A>>(result);
                } else {
                    var completion = new TaskCompletionSource<IEnumerable<A>>();
                    BulkRequests.Enqueue(completion);
                    _ = token.Register(() => completion.TrySetCanceled());
                    resultTask = completion.Task;
                }
                if (locked > 0) {
                    _ = Mutex.Release(locked);
                }
                locked = 0;
                return await resultTask;
            }
            finally {
                if (locked > 0) {
                    _ = Mutex.Release(locked);
                }
            }
        }
        /// <summary>
        /// Dequeue all A's as soon as any are available
        /// </summary>
        /// <returns>A task that will complete with all A's available</returns>
        public Task<IEnumerable<A>> DequeueAsync() => DequeueAsync(CancellationToken.None);
    }
}
