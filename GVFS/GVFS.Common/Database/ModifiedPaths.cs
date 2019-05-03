using GVFS.Common.Tracing;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;

namespace GVFS.Common.Database
{
    public class ModifiedPaths
    {
        private ITracer tracer;
        private SqliteConnection connection;
        private ConcurrentHashSet<string> modifiedPathsCache;

        public ModifiedPaths(ITracer tracer, SqliteConnection connection)
        {
            this.tracer = tracer;
            this.connection = connection;
            this.modifiedPathsCache = new ConcurrentHashSet<string>(StringComparer.OrdinalIgnoreCase);

            using (SqliteCommand command = this.connection.CreateCommand())
            {
                foreach (string modifiedPath in ModifiedPaths.GetAll(command))
                {
                    this.modifiedPathsCache.Add(modifiedPath);
                }
            }
        }

        public int Count
        {
            get { return this.modifiedPathsCache.Count; }
        }

        public static void CreateTable(SqliteCommand command)
        {
            command.CommandText = @"CREATE TABLE IF NOT EXISTS [ModifiedPaths] (path TEXT PRIMARY KEY ) WITHOUT ROWID;";
            command.ExecuteNonQuery();
            Insert(command, GVFSConstants.SpecialGitFiles.GitAttributes);
        }

        public void RemoveEntriesWithParentFolderEntry()
        {
            int startingCount = this.modifiedPathsCache.Count;
            using (ITracer activity = this.tracer.StartActivity(nameof(this.RemoveEntriesWithParentFolderEntry), EventLevel.Informational))
            using (SqliteCommand command = this.connection.CreateCommand())
            {
                foreach (string modifiedPath in this.modifiedPathsCache)
                {
                    if (this.ModifiedPathsContainsParentDirectory(modifiedPath))
                    {
                        Delete(command, modifiedPath);
                        this.modifiedPathsCache.TryRemove(modifiedPath);
                    }
                }

                EventMetadata metadata = new EventMetadata();
                metadata.Add(nameof(startingCount), startingCount);
                metadata.Add("EndCount", this.modifiedPathsCache.Count);
                activity.Stop(metadata);
            }
        }

        public bool TryRemove(string path, bool isFolder, out bool isRetryable)
        {
            isRetryable = true;
            string normalizedPath = this.NormalizeEntryString(path, isFolder);
            if (this.modifiedPathsCache.Contains(normalizedPath))
            {
                using (SqliteCommand command = this.connection.CreateCommand())
                {
                    ModifiedPaths.Delete(command, normalizedPath);
                    this.modifiedPathsCache.TryRemove(normalizedPath);
                }
            }

            return true;
        }

        public bool TryAdd(string path, bool isFolder, out bool isRetryable)
        {
            isRetryable = true;
            string normalizedPath = this.NormalizeEntryString(path, isFolder);
            if (!this.modifiedPathsCache.Contains(normalizedPath) && !this.ModifiedPathsContainsParentDirectory(normalizedPath))
            {
                using (SqliteCommand command = this.connection.CreateCommand())
                {
                    ModifiedPaths.Insert(command, normalizedPath);
                    this.modifiedPathsCache.Add(normalizedPath);
                }
            }

            return true;
        }

        public bool Contains(string path, bool isFolder)
        {
            string entry = this.NormalizeEntryString(path, isFolder);
            return this.modifiedPathsCache.Contains(entry);
        }

        public IEnumerable<string> GetAll()
        {
            return this.modifiedPathsCache;
        }

        private static void Insert(SqliteCommand command, string modifiedPath)
        {
            command.Parameters.AddWithValue("@path", modifiedPath);
            command.CommandText = $"INSERT OR IGNORE INTO ModifiedPaths (path) VALUES (@path);";
            command.ExecuteNonQuery();
        }

        private static string[] GetAll(SqliteCommand command)
        {
            List<string> pathList = new List<string>();
            command.CommandText = $"SELECT path FROM ModifiedPaths;";
            using (SqliteDataReader reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    pathList.Add(reader.GetString(0));
                }
            }

            return pathList.ToArray();
        }

        private static void Delete(SqliteCommand command, string modifiedPath)
        {
            command.Parameters.AddWithValue("@path", modifiedPath);
            command.CommandText = $"DELETE FROM ModifiedPaths WHERE path = @path;";
            command.ExecuteNonQuery();
        }

        private bool ModifiedPathsContainsParentDirectory(string modifiedPath)
        {
            string[] pathParts = modifiedPath.Split(new char[] { GVFSConstants.GitPathSeparator }, StringSplitOptions.RemoveEmptyEntries);
            string parentFolder = string.Empty;
            for (int i = 0; i < pathParts.Length - 1; i++)
            {
                parentFolder += pathParts[i] + GVFSConstants.GitPathSeparatorString;
                if (this.modifiedPathsCache.Contains(parentFolder))
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
