using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace Hotsapi.Uploader.Common
{
    internal class ReplayUpload : HttpContent
    {
        private const int defaultBuffersize = 1024;
        private readonly string filename;
        private readonly int buffersize;
        private readonly Task mayComplete;
        
        public ReplayUpload(string filename, int buffersize, Task mayComplete)
        {
            this.filename = filename;
            this.buffersize = buffersize;
            this.mayComplete = mayComplete;
        }

        public ReplayUpload(string filename, int buffersize) : this(filename, buffersize, Task.CompletedTask) { }
        public ReplayUpload(string filename, Task canComplete) : this(filename, defaultBuffersize, canComplete) { }
        public ReplayUpload(string filename) : this(filename, defaultBuffersize, Task.CompletedTask) { }


        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext context)
        {
            using (var input = File.OpenRead(filename)) {
                var buffer = new byte[buffersize];
                var i = 0;
                var done = false;
                while (!done) {
                    var availableSpace = buffer.Length - i;
                    var bytesRead = await input.ReadAsync(buffer, i, availableSpace);
                    if (bytesRead == 0) {
                        done = true;
                    }
                    if (bytesRead < availableSpace) {
                        await mayComplete;
                    }
                    await stream.WriteAsync(buffer, i, bytesRead);
                    i = 0;
                }
                await stream.FlushAsync();
                input.Close();
            }
        }
        protected override bool TryComputeLength(out long length)
        {
            length = -1;
            return false;
        }
    }

}
