using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Hotsapi.Uploader.Common.Test
{
    [TestClass]
    public partial class ManagerTests
    {
        public static Task ShortRandomDelay()
        {
            var r = new Random();
            var delay = r.Next(100, 200);
            return Task.Delay(delay);
        }
        private static IEnumerable<ReplayFile> FilesInOrder
        {
            get {
                var next = new DateTime(2020, 1, 1, 0, 0, 0);
                var increment = new TimeSpan(0, 0, 1);
                var rand = new Random();
                var nums = Enumerable.Range(1, 24).OrderBy(rf => rand.NextDouble());
                
                return nums.Select(i => {
                    next += increment;
                    return new ReplayFile($"upload_{i}") {
                        Created = next
                    };
                });

            }
        }

        [TestMethod]
        public async Task InitialFilesEndInorder() {
            var initialFiles = FilesInOrder;

            var manager = new Manager(new MockStorage(initialFiles));
            var uploadTester = new MockUploader();
            var promise = new TaskCompletionSource<int>();
            Task done = promise.Task;

            var uploadsSeen = 0;
            var l = new object();
            ReplayFile lastUploadFinished = null;
            uploadTester.UploadFinished = async rf => {
                if (lastUploadFinished != null) {
                    try {
                        var isInOrder = rf.Created >= lastUploadFinished.Created;
                        Assert.IsTrue(isInOrder, $"upload completed out of order, {rf} completed after {lastUploadFinished}");
                    }
                    catch (Exception e) {
                        promise.TrySetException(e);
                    }
                }
                lastUploadFinished = rf;
                var isDone = false;
                lock (l) {
                    uploadsSeen++;
                    isDone = uploadsSeen >= 3;
                }
                if (isDone) {
                    promise.TrySetResult(uploadsSeen);
                }
            };

            manager.Start(new NoNewFilesMonitor(), new MockAnalizer(), uploadTester);
            await done;
        }

        [TestMethod]
        public async Task AllInitialFilesProcessed()
        {
            var initialFiles = FilesInOrder;

            var manager = new Manager(new MockStorage(initialFiles));
            var uploadTester = new MockUploader();
            var done = new TaskCompletionSource<int>();

            var uploadsSeen = 0;
            object l = new object();
            uploadTester.UploadFinished = async rf => {
                await ShortRandomDelay();
                lock (l) {
                    uploadsSeen++;
                    if (uploadsSeen >= 3) {
                        done.SetResult(uploadsSeen);
                    }
                }
            };

            manager.Start(new NoNewFilesMonitor(), new MockAnalizer(), uploadTester);
            var num = await done.Task;
            Assert.AreEqual(3, num);
        }
    }
}
