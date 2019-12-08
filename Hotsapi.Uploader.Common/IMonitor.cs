using System;
using System.Collections.Generic;

namespace Hotsapi.Uploader.Common
{
    public interface IMonitor
    {
        event EventHandler<EventArgs<string>> ReplayAdded;

        IEnumerable<string> ScanReplays();
        void Start();
        void Stop();
    }
}