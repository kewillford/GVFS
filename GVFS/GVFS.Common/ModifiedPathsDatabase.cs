using System;
using System.Collections.Generic;
using System.IO;
using GVFS.Common.Database;
using GVFS.Common.FileSystem;
using GVFS.Common.Tracing;

namespace GVFS.Common
{
    /// <summary>
    /// The modified paths database is the list of files and folders that
    /// git is now responsible for keeping up to date. Files and folders are added
    /// to this list by being created, edited, deleted, or renamed.
    /// </summary>
    public class ModifiedPathsDatabase : IDisposable
    {
        private ConcurrentHashSet<string> modifiedPaths;
        private ModifiedPathsStore modifiedPathsStore;

        public ModifiedPathsDatabase(ITracer tracer, PhysicalFileSystem fileSystem, string enlistmentRoot)
        {
            this.modifiedPaths = new ConcurrentHashSet<string>(StringComparer.OrdinalIgnoreCase);
            this.modifiedPathsStore = new ModifiedPathsStore(tracer, fileSystem, enlistmentRoot);
            this.modifiedPathsStore.TryGetAll(out string[] paths);

            if (paths.Length == 0)
            {
                this.TryAdd(GVFSConstants.SpecialGitFiles.GitAttributes, isFolder: false, isRetryable: out bool isRetryable);
            }

            foreach (string path in paths)
            {
                this.modifiedPaths.Add(path);
            }
        }

        public int Count
        {
            get { return this.modifiedPaths.Count; }
        }

        /// <summary>
        /// This method will examine the modified paths to check if there is already a parent folder entry in
        /// the modified paths.  If there is a parent folder the entry does not need to be in the modified paths
        /// and will be removed because the parent folder is recursive and covers any children.
        /// </summary>
        public void RemoveEntriesWithParentFolderEntry(ITracer tracer)
        {
            int startingCount = this.modifiedPaths.Count;
            using (ITracer activity = tracer.StartActivity(nameof(this.RemoveEntriesWithParentFolderEntry), EventLevel.Informational))
            {
                foreach (string modifiedPath in this.modifiedPaths)
                {
                    if (this.ContainsParentDirectory(modifiedPath))
                    {
                        this.modifiedPathsStore.TryRemove(modifiedPath);
                        this.modifiedPaths.TryRemove(modifiedPath);
                    }
                }

                EventMetadata metadata = new EventMetadata();
                metadata.Add(nameof(startingCount), startingCount);
                metadata.Add("EndCount", this.modifiedPaths.Count);
                activity.Stop(metadata);
            }
        }

        public bool Contains(string path, bool isFolder)
        {
            string entry = this.NormalizeEntryString(path, isFolder);
            return this.modifiedPaths.Contains(entry);
        }

        public IEnumerable<string> GetAllModifiedPaths()
        {
            return this.modifiedPaths;
        }

        public bool TryAdd(string path, bool isFolder, out bool isRetryable)
        {
            isRetryable = true;
            string entry = this.NormalizeEntryString(path, isFolder);
            if (!this.modifiedPaths.Contains(entry) && !this.ContainsParentDirectory(entry))
            {
                this.modifiedPathsStore.TryAdd(entry);
                this.modifiedPaths.Add(entry);
            }

            return true;
        }

        public bool TryRemove(string path, bool isFolder, out bool isRetryable)
        {
            isRetryable = true;
            string entry = this.NormalizeEntryString(path, isFolder);
            if (this.modifiedPaths.Contains(entry))
            {
                isRetryable = true;
                this.modifiedPathsStore.TryRemove(entry);
                this.modifiedPaths.TryRemove(entry);
            }

            return true;
        }

        public void Dispose()
        {
            this.modifiedPathsStore?.Dispose();
            this.modifiedPathsStore = null;
        }

        private bool ContainsParentDirectory(string modifiedPath)
        {
            string[] pathParts = modifiedPath.Split(new char[] { GVFSConstants.GitPathSeparator }, StringSplitOptions.RemoveEmptyEntries);
            string parentFolder = string.Empty;
            for (int i = 0; i < pathParts.Length - 1; i++)
            {
                parentFolder += pathParts[i] + GVFSConstants.GitPathSeparatorString;
                if (this.modifiedPaths.Contains(parentFolder))
                {
                    return true;
                }
            }

            return false;
        }

        private string NormalizeEntryString(string virtualPath, bool isFolder)
        {
            // TODO(Mac) This can be optimized if needed
            return virtualPath.Replace(Path.DirectorySeparatorChar, GVFSConstants.GitPathSeparator).Trim(GVFSConstants.GitPathSeparator) +
                (isFolder ? GVFSConstants.GitPathSeparatorString : string.Empty);
        }
    }
}
