using System;
using System.Threading;
using System.Threading.Tasks;

namespace Hotsapi.Uploader.Common
{
    /// <summary>
    /// Provides extensions for using SemaphoreSlim in a safe way that will always release the semaphore
    /// </summary>
    public static class LockExtensions
    {
        public static async Task<A> Locked<A>(this SemaphoreSlim semaphore, Func<A> thunk, CancellationToken token)
        {
            var locked = 0;
            try {
                await semaphore.WaitAsync(token);
                locked = 1;
                return thunk();
            }
            finally {
                if (locked > 0) {
                    _ = semaphore.Release(locked);
                }
            }
        }

        public static async Task Locked(this SemaphoreSlim semaphore, Action thunk, CancellationToken token)
        {
            var locked = 0;
            try {
                await semaphore.WaitAsync(token);
                locked = 1;
                thunk();
            }
            finally {
                if (locked > 0) {
                    _ = semaphore.Release(locked);
                }
            }
        }

        public static async Task LockedTask(this SemaphoreSlim semaphore, Func<Task> thunk, CancellationToken token)
        {
            var locked = 0;
            try {
                await semaphore.WaitAsync(token);
                locked = 1;
                await thunk();
            }
            finally {
                if (locked > 0) {
                    _ = semaphore.Release(locked);
                }
            }
        }

        public static async Task<A> LockedTask<A>(this SemaphoreSlim semaphore, Func<Task<A>> thunk, CancellationToken token)
        {
            var locked = 0;
            try {
                await semaphore.WaitAsync(token);
                locked = 1;
                return await thunk();
            }
            finally {
                if (locked > 0) {
                    _ = semaphore.Release(locked);
                }
            }
        }

        public static Task<A> Locked<A>(this SemaphoreSlim semaphore, Func<A> thunk) => Locked(semaphore, thunk, CancellationToken.None);
        public static Task Locked(this SemaphoreSlim semaphore, Action thunk) => Locked(semaphore, thunk, CancellationToken.None);
        public static Task<A> LockedTask<A>(this SemaphoreSlim semaphore, Func<Task<A>> thunk) => LockedTask(semaphore, thunk, CancellationToken.None);
        public static Task LockedTask(this SemaphoreSlim semaphore, Func<Task> thunk) => LockedTask(semaphore, thunk, CancellationToken.None);
    }
}
