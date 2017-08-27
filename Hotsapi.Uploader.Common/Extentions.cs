using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hotsapi.Uploader.Common
{
    public static class Extentions
    {
        /// <summary>
        /// Executes specified delegate on all members of the collection
        /// </summary>
        public static void Map<T>(this IEnumerable<T> src, Action<T> action)
        {
            src.Select(q => { action(q); return 0; }).Count();
        }
    }
}
