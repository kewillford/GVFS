using GVFS.Common.FileSystem;
using GVFS.Common.Tracing;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Concurrent;
using System.Data;
using System.IO;

namespace GVFS.Common.Database
{
    public class GVFSDatabase : IGVFSConnectionPool, IDisposable
    {
        private const int InitialPooledConnections = 5;
        private const int MillisecondsWaitingToGetConnection = 50;

        private bool disposed = false;
        private ITracer tracer;
        private string databasePath;
        private string sqliteConnectionString;
        private BlockingCollection<IDbConnection> connectionPool;

        public GVFSDatabase(ITracer tracer, PhysicalFileSystem fileSystem, string enlistmentRoot)
        {
            this.tracer = tracer;
            this.connectionPool = new BlockingCollection<IDbConnection>();
            this.databasePath = Path.Combine(enlistmentRoot, GVFSConstants.DotGVFS.Root, GVFSConstants.DotGVFS.Databases.GVFSDatabase);
            this.sqliteConnectionString = SqliteDatabase.CreateConnectionString(this.databasePath);

            string folderPath = Path.GetDirectoryName(this.databasePath);
            fileSystem.CreateDirectory(folderPath);

            for (int i = 0; i < InitialPooledConnections; i++)
            {
                this.connectionPool.Add(this.OpenNewConnection());
            }

            this.Initialize();
            this.CreateTables();
        }

        public void Dispose()
        {
            this.disposed = true;
            this.connectionPool.CompleteAdding();
            while (!this.connectionPool.IsCompleted && this.connectionPool.TryTake(out IDbConnection connection))
            {
                connection.Dispose();
            }
        }

        IPooledConnection IGVFSConnectionPool.GetConnection()
        {
            IDbConnection connection;
            if (!this.connectionPool.TryTake(out connection, millisecondsTimeout: MillisecondsWaitingToGetConnection))
            {
                connection = this.OpenNewConnection();
            }

            return new GVFSConnection(this, connection);
        }

        void IGVFSConnectionPool.ReturnToPool(IDbConnection connection)
        {
            if (this.disposed)
            {
                connection?.Dispose();
            }
            else if (!this.connectionPool.TryAdd(connection))
            {
                connection?.Dispose();
            }
        }

        private void Initialize()
        {
            IGVFSConnectionPool connectionPool = this;
            using (IPooledConnection pooled = connectionPool.GetConnection())
            using (IDbCommand command = pooled.Connection.CreateCommand())
            {
                command.CommandText = $"PRAGMA journal_mode=WAL;";
                command.ExecuteNonQuery();
                command.CommandText = $"PRAGMA cache_size=-40000;";
                command.ExecuteNonQuery();
                command.CommandText = $"PRAGMA synchronous=NORMAL;";
                command.ExecuteNonQuery();
                command.CommandText = $"PRAGMA user_version;";
                object userVersion = command.ExecuteScalar();
                if (userVersion == null || Convert.ToInt64(userVersion) < 1)
                {
                    command.CommandText = $"PRAGMA user_version=1;";
                    command.ExecuteNonQuery();
                }
            }
        }

        private void CreateTables()
        {
            IGVFSConnectionPool connectionPool = this;
            using (IPooledConnection pooled = connectionPool.GetConnection())
            using (IDbCommand command = pooled.Connection.CreateCommand())
            {
                Placeholders.CreateTable(command);
            }
        }

        private SqliteConnection OpenNewConnection()
        {
            SqliteConnection connection = new SqliteConnection(this.sqliteConnectionString);
            connection.Open();
            return connection;
        }

        private class GVFSConnection : IPooledConnection
        {
            private IDbConnection connection;
            private IGVFSConnectionPool database;

            public GVFSConnection(IGVFSConnectionPool database, IDbConnection connection)
            {
                this.database = database;
                this.connection = connection;
            }

            IDbConnection IPooledConnection.Connection => this.connection;

            public void Dispose()
            {
                this.database.ReturnToPool(this.connection);
            }
        }
    }
}
