using GVFS.Common.Tracing;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace GVFS.Common
{
    public class MultiStopwatch
    {
        private ConcurrentDictionary<string, Stopwatch> stopwatches;

        public MultiStopwatch()
        {
            this.stopwatches = new ConcurrentDictionary<string, Stopwatch>();
        }

        public void Clear()
        {
            this.stopwatches.Clear();
        }

        public IDisposable Start(string key)
        {
            if (!this.stopwatches.ContainsKey(key))
            {
                this.stopwatches.TryAdd(key, Stopwatch.StartNew());
            }
            else
            {
                this.stopwatches[key].Start();
            }

            return new AutoStop(this.stopwatches[key]);
        }

        public EventMetadata GetMetadata(string area)
        {
            EventMetadata metadata = new EventMetadata();
            metadata.Add("Area", area);
            foreach (KeyValuePair<string, Stopwatch> timer in this.stopwatches)
            {
                metadata.Add(timer.Key + "_TotalElapsed", timer.Value.Elapsed);
            }

            return metadata;
        }

        private class AutoStop : IDisposable
        {
            private Stopwatch stopwatch;

            public AutoStop(Stopwatch stopwatch)
            {
                this.stopwatch = stopwatch;
            }

            void IDisposable.Dispose()
            {
                this.stopwatch.Stop();
            }
        }
    }
}
