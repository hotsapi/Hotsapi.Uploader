using System;
using System.Collections.Generic;
using System.Linq;

namespace Hotsapi.Uploader.Common
{
    public interface IReplayStorage
    {
        void Save(IEnumerable<ReplayFile> files);
        IEnumerable<ReplayFile> Load();
    }
}
