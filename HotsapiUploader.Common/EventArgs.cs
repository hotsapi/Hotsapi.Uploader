using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HotsapiUploader.Common
{
    public class EventArgs<T> : EventArgs
    {
        public T Data { get; private set; }

        public EventArgs(T input)
        {
            Data = input;
        }
    }
}
