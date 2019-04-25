using GVFS.Common.FileSystem;
using GVFS.Common.Tracing;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace GVFS.Common.Database
{
    public class GVFSDatabase : IDisposable
    {
        private readonly SqliteConnection connection;
        private PhysicalFileSystem fileSystem;
        private ITracer tracer;
        private string databasePath;
        private string sqliteConnectionString;

        public GVFSDatabase(ITracer tracer, PhysicalFileSystem fileSystem, string enlistmentRoot)
        {
            this.tracer = tracer;
            this.fileSystem = fileSystem;
            this.databasePath = Path.Combine(enlistmentRoot, GVFSConstants.DotGVFS.Root, GVFSConstants.DotGVFS.Databases.SqliteData);
            this.sqliteConnectionString = $"data source={this.databasePath};Cache=Shared";

            string folderPath = Path.GetDirectoryName(this.databasePath);
            this.fileSystem.CreateDirectory(folderPath);

            bool databaseInitialized = fileSystem.FileExists(this.databasePath);

            this.connection = new SqliteConnection(this.sqliteConnectionString);
            this.connection.Open();

            if (!databaseInitialized)
            {
                this.Initialize();
            }
        }

        public void Dispose()
        {
            this.connection?.Dispose();
        }

        public void RemoveEntriesWithParentFolderEntry()
        {
        }

        public string[] GetAllModifiedPaths()
        {
        }

        private void Initialize()
        {
            using (SqliteCommand command = this.connection.CreateCommand())
            {
                command.CommandText = $"PRAGMA journal_mode=WAL;";
                command.ExecuteNonQuery();
                command.CommandText = $"PRAGMA cache_size=-40000;";
                command.ExecuteNonQuery();
                command.CommandText = $"PRAGMA synchronous=FULL;";
                command.ExecuteNonQuery();
                command.CommandText = $"PRAGMA user_version;";
                object userVersion = command.ExecuteScalar();
                if (userVersion == null || Convert.ToInt64(userVersion) < 1)
                {
                    command.CommandText = $"PRAGMA user_version=1;";
                    command.ExecuteNonQuery();
                }

                command.CommandText = @"CREATE TABLE IF NOT EXISTS [ModifiedPaths] (path TEXT PRIMARY KEY ) WITHOUT ROWID;";
                command.ExecuteNonQuery();
            }
        }

        private class PlaceholderList
        {
            public static void Insert(SqliteCommand command, string modifiedPath)
            {
                command.Parameters.AddWithValue("@path", modifiedPath);
                command.CommandText = $"INSERT OR IGNORE INTO PlaceholderList (path) VALUES (@path);";
                command.ExecuteNonQuery();
            }

            public static string[] GetAll(SqliteCommand command)
            {
                List<string> pathList = new List<string>();
                command.CommandText = $"SELECT path FROM PlaceholderList;";
                using (SqliteDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        pathList.Add(reader.GetString(0));
                    }
                }

                return pathList.ToArray();
            }

            public static void Delete(SqliteCommand command, string modifiedPath)
            {
                command.Parameters.AddWithValue("@path", modifiedPath);
                command.CommandText = $"DELETE FROM PlaceholderList WHERE path = @path;";
                command.ExecuteNonQuery();
            }
        }

        private class ModifiedPaths
        {
            public static void Insert(SqliteCommand command, string modifiedPath)
            {
                command.Parameters.AddWithValue("@path", modifiedPath);
                command.CommandText = $"INSERT OR IGNORE INTO ModifiedPaths (path) VALUES (@path);";
                command.ExecuteNonQuery();
            }

            public static string[] GetAll(SqliteCommand command)
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

            public static void Delete(SqliteCommand command, string modifiedPath)
            {
                command.Parameters.AddWithValue("@path", modifiedPath);
                command.CommandText = $"DELETE FROM ModifiedPaths WHERE path = @path;";
                command.ExecuteNonQuery();
            }
        }
    }
}
