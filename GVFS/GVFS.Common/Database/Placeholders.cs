using GVFS.Common.Tracing;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;

namespace GVFS.Common.Database
{
    public class Placeholders : IFilePlaceholderStore
    {
        private SqliteConnection connection;
        private ITracer tracer;

        public Placeholders(ITracer tracer, SqliteConnection connection)
        {
            this.tracer = tracer;
            this.connection = connection;
        }

        public static void CreateTable(SqliteCommand command)
        {
            command.CommandText = @"CREATE TABLE IF NOT EXISTS [Placeholders] (path TEXT PRIMARY KEY, pathType TINYINT DEFAULT 0, sha char(40) ) WITHOUT ROWID;";
            command.ExecuteNonQuery();
        }

        public int Count()
        {
            using (ITracer activity = this.tracer.StartActivity("Placeholders.Count", EventLevel.Informational))
            using (SqliteCommand command = this.connection.CreateCommand())
            {
                command.CommandText = $"SELECT count(path) FROM Placeholders;";
                return Convert.ToInt32(command.ExecuteScalar());
            }
        }

        public void GetAllEntries(out List<IPlaceholder> filePlaceholders, out List<IPlaceholder> folderPlaceholders)
        {
            filePlaceholders = new List<IPlaceholder>();
            folderPlaceholders = new List<IPlaceholder>();
            using (ITracer activity = this.tracer.StartActivity("Placeholders.GetAllEntries", EventLevel.Informational))
            using (SqliteCommand command = this.connection.CreateCommand())
            {
                command.CommandText = $"SELECT path, pathType, sha FROM Placeholders;";
                using (SqliteDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        PlaceholderData data = new PlaceholderData();
                        data.Path = reader.GetString(0);
                        data.PathType = (PlaceholderData.PlaceholderType)reader.GetByte(1);

                        if (!reader.IsDBNull(2))
                        {
                            data.Sha = reader.GetString(2);
                        }

                        if (data.PathType == PlaceholderData.PlaceholderType.File)
                        {
                            filePlaceholders.Add(data);
                        }
                        else
                        {
                            folderPlaceholders.Add(data);
                        }
                    }
                }
            }
        }

        public HashSet<string> GetAllFileEntries()
        {
            using (ITracer activity = this.tracer.StartActivity("Placeholders.GetAllFileEntries", EventLevel.Informational))
            using (SqliteCommand command = this.connection.CreateCommand())
            {
                HashSet<string> fileEntries = new HashSet<string>();
                command.CommandText = $"SELECT path FROM Placeholders WHERE pathType = 0;";
                using (SqliteDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        fileEntries.Add(reader.GetString(0));
                    }
                }

                return fileEntries;
            }
        }

        public bool Contains(string path)
        {
            using (SqliteCommand command = this.connection.CreateCommand())
            {
                command.Parameters.AddWithValue("@path", path);
                command.CommandText = $"SELECT 1 FROM Placeholders WHERE path = @path;";
                object result = command.ExecuteScalar();
                return !(result == DBNull.Value);
            }
        }

        public void AddFilePlaceholder(string path, string sha)
        {
            using (SqliteCommand command = this.connection.CreateCommand())
            {
                Insert(command, new PlaceholderData() { Path = path, PathType = PlaceholderData.PlaceholderType.File, Sha = sha });
            }
        }

        public void AddAndFlushFolder(string path, bool isExpanded)
        {
            PlaceholderData placeholderData = new PlaceholderData() { Path = path, PathType = PlaceholderData.PlaceholderType.PartialFolder };
            if (isExpanded)
            {
                placeholderData.PathType = PlaceholderData.PlaceholderType.ExpandedFolder;
            }

            using (SqliteCommand command = this.connection.CreateCommand())
            {
                Insert(command, placeholderData);
            }
        }

        public void AddAndFlushPossibleTombstoneFolder(string path)
        {
            using (SqliteCommand command = this.connection.CreateCommand())
            {
                Insert(command, new PlaceholderData() { Path = path, PathType = PlaceholderData.PlaceholderType.PossibleTombstoneFolder });
            }
        }

        public void RemoveFilePlaceholder(string path)
        {
            this.Remove(path);
        }

        public void Remove(string path)
        {
            using (SqliteCommand command = this.connection.CreateCommand())
            {
                Delete(command, path);
            }
        }

        private static void Insert(SqliteCommand command, PlaceholderData placeholder)
        {
            command.Parameters.AddWithValue("@path", placeholder.Path);
            command.Parameters.AddWithValue("@pathType", placeholder.PathType);

            if (placeholder.Sha == null)
            {
                command.Parameters.AddWithValue("@sha", DBNull.Value);
            }
            else
            {
                command.Parameters.AddWithValue("@sha", placeholder.Sha);
            }

            command.CommandText = $"INSERT OR REPLACE INTO Placeholders (path, pathType, sha) VALUES (@path, @pathType, @sha);";
            command.ExecuteNonQuery();
        }

        private static void Delete(SqliteCommand command, string path)
        {
            command.Parameters.AddWithValue("@path", path);
            command.CommandText = $"DELETE FROM Placeholders WHERE path = @path;";
            command.ExecuteNonQuery();
        }

        public class PlaceholderData : IPlaceholder
        {
            public enum PlaceholderType : byte
            {
                File = 0,
                PartialFolder = 1,
                ExpandedFolder = 2,
                PossibleTombstoneFolder = 3,
            }

            public string Path { get; set; }
            public PlaceholderType PathType { get; set; }
            public string Sha { get; set; }

            public bool IsFolder => this.PathType != PlaceholderType.File;

            public bool IsExpandedFolder => this.PathType == PlaceholderType.ExpandedFolder;

            public bool IsPossibleTombstoneFolder => this.PathType == PlaceholderType.PossibleTombstoneFolder;
        }
    }
}
