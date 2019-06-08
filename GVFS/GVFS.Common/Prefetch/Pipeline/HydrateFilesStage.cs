using GVFS.Common.FileSystem;
using GVFS.Common.Git;
using GVFS.Common.Prefetch.Git;
using GVFS.Common.Tracing;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace GVFS.Common.Prefetch.Pipeline
{
    public class HydrateFilesStage : PrefetchPipelineStage
    {
        private readonly ConcurrentDictionary<string, HashSet<PathWithMode>> blobIdToPaths;
        private readonly BlockingCollection<string> availableBlobs;

        private ITracer tracer;
        private int readFileCount;
        private GitRepo repo;

        public HydrateFilesStage(int maxThreads, ConcurrentDictionary<string, HashSet<PathWithMode>> blobIdToPaths, BlockingCollection<string> availableBlobs, ITracer tracer, GitRepo repo)
            : base(maxThreads)
        {
            this.blobIdToPaths = blobIdToPaths;
            this.availableBlobs = availableBlobs;

            this.repo = repo;
            this.tracer = tracer;
        }

        public int ReadFileCount
        {
            get { return this.readFileCount; }
        }

        protected override void DoWork()
        {
            using (ITracer activity = this.tracer.StartActivity("ReadFiles", EventLevel.Informational))
            {
                int readFilesCurrentThread = 0;
                int failedFilesCurrentThread = 0;
                PhysicalFileSystem fileSystem = new PhysicalFileSystem();

                string blobId;
                while (this.availableBlobs.TryTake(out blobId, Timeout.Infinite))
                {
                    foreach (PathWithMode modeAndPath in this.blobIdToPaths[blobId])
                    {
                        bool succeeded = this.repo.TryCopyBlobContentStream(
                            blobId,
                            (stream, size) =>
                            {
                                fileSystem.CreateDirectory(Path.GetDirectoryName(modeAndPath.Path));
                                using (FileStream outStream = File.OpenWrite(modeAndPath.Path))
                                {
                                    stream.CopyToAsync(outStream).Wait();
                                }
                            });

                        if (succeeded)
                        {
                            Interlocked.Increment(ref this.readFileCount);
                            readFilesCurrentThread++;
                        }
                        else
                        {
                            activity.RelatedError("Failed to read " + modeAndPath.Path);

                            failedFilesCurrentThread++;
                            this.HasFailures = true;
                        }
                    }
                }

                activity.Stop(
                    new EventMetadata
                    {
                        { "FilesRead", readFilesCurrentThread },
                        { "Failures", failedFilesCurrentThread },
                    });
            }
        }
    }
}
