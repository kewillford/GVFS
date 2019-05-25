using GVFS.Common.FileSystem;
using GVFS.Common.Tracing;
using System;
using System.Collections.Concurrent;
using System.Data;
using System.IO;

namespace GVFS.Common.Database
{
    /// <summary>
    /// Handles setting up the database for storing data used by GVFS and
    /// managing the connections to the database
    /// </summary>
    public class GVFSDatabase : IGVFSConnectionPool, IDisposable
    {
        private const int InitialPooledConnections = 5;
        private const int MillisecondsWaitingToGetConnection = 50;

        private bool disposed = false;
        private string databasePath;
        private IDbConnectionFactory connectionFactory;
        private BlockingCollection<IGVFSConnection> connectionPool;

        public GVFSDatabase(PhysicalFileSystem fileSystem, string enlistmentRoot, IDbConnectionFactory connectionFactory, int initialPooledConnections = InitialPooledConnections)
        {
            this.connectionPool = new BlockingCollection<IGVFSConnection>();
            this.databasePath = Path.Combine(enlistmentRoot, GVFSPlatform.Instance.Constants.DotGVFSRoot, GVFSConstants.DotGVFS.Databases.VFSForGit);
            this.connectionFactory = connectionFactory;

            string folderPath = Path.GetDirectoryName(this.databasePath);
            fileSystem.CreateDirectory(folderPath);

            try
            {
                this.Initialize();

                for (int i = 0; i < initialPooledConnections; i++)
                {
                    IDbConnection connection = this.connectionFactory.OpenNewConnection(this.databasePath);
                    this.connectionPool.Add(new GVFSConnection(this, connection, PlaceholderTable.PrepareInsert(connection)));
                }
            }
            catch (Exception ex)
            {
                throw new GVFSDatabaseException($"{nameof(GVFSDatabase)} constructor threw exception setting up connection pool and initializing", ex);
            }
        }

        public interface IGVFSConnection : IDbConnection
        {
            IDbCommand GetPreparedInsert();
            void DisposeConnection();
        }

        public void Dispose()
        {
            if (this.disposed)
            {
                return;
            }

            this.disposed = true;
            this.connectionPool.CompleteAdding();
            while (!this.connectionPool.IsCompleted && this.connectionPool.TryTake(out IGVFSConnection connection))
            {
                connection.Dispose();
            }

            this.connectionPool.Dispose();
            this.connectionPool = null;
        }

        IGVFSConnection IGVFSConnectionPool.GetConnection()
        {
            if (this.disposed)
            {
                throw new ObjectDisposedException(nameof(GVFSDatabase));
            }

            IGVFSConnection gvfsConnection;
            if (!this.connectionPool.TryTake(out gvfsConnection, millisecondsTimeout: MillisecondsWaitingToGetConnection))
            {
                IDbConnection connection = this.connectionFactory.OpenNewConnection(this.databasePath);
                gvfsConnection = new GVFSConnection(this, connection, PlaceholderTable.PrepareInsert(connection));
            }

            return gvfsConnection;
        }

        private void ReturnToPool(IGVFSConnection connection)
        {
            if (this.connectionPool.IsAddingCompleted)
            {
                connection.Dispose();
                return;
            }

            bool itemWasAdded = false;
            try
            {
                itemWasAdded = this.connectionPool.TryAdd(connection);
            }
            catch (InvalidOperationException)
            {
                itemWasAdded = false;
            }

            if (!itemWasAdded)
            {
                connection.Dispose();
            }
        }

        private void Initialize()
        {
            IGVFSConnectionPool connectionPool = this;
            using (IDbConnection connection = this.connectionFactory.OpenNewConnection(this.databasePath))
            using (IDbCommand command = connection.CreateCommand())
            {
                command.CommandText = "PRAGMA journal_mode=WAL;";
                command.ExecuteNonQuery();
                command.CommandText = "PRAGMA cache_size=-40000;";
                command.ExecuteNonQuery();
                command.CommandText = "PRAGMA synchronous=NORMAL;";
                command.ExecuteNonQuery();
                command.CommandText = "PRAGMA user_version;";
                object userVersion = command.ExecuteScalar();
                if (userVersion == null || Convert.ToInt64(userVersion) < 1)
                {
                    command.CommandText = "PRAGMA user_version=1;";
                    command.ExecuteNonQuery();
                }

                PlaceholderTable.CreateTable(connection);
            }
        }

        /// <summary>
        /// This class is used to wrap a IDbConnection and return it to the connection pool when disposed
        /// </summary>
        private class GVFSConnection : IGVFSConnection
        {
            private IDbConnection connection;
            private IDbCommand preparedInsert;
            private GVFSDatabase database;

            public GVFSConnection(GVFSDatabase database, IDbConnection connection, IDbCommand preparedInsert)
            {
                this.database = database;
                this.connection = connection;
                this.preparedInsert = preparedInsert;
            }

            public string ConnectionString { get => this.connection.ConnectionString; set => this.connection.ConnectionString = value; }

            public int ConnectionTimeout => this.connection.ConnectionTimeout;

            public string Database => this.connection.Database;

            public ConnectionState State => this.connection.State;

            public IDbTransaction BeginTransaction()
            {
                return this.connection.BeginTransaction();
            }

            public IDbTransaction BeginTransaction(IsolationLevel il)
            {
                return this.connection.BeginTransaction(il);
            }

            public void ChangeDatabase(string databaseName)
            {
                this.connection.ChangeDatabase(databaseName);
            }

            public void Close()
            {
                this.connection.Close();
            }

            public IDbCommand CreateCommand()
            {
                return this.connection.CreateCommand();
            }

            public void Dispose()
            {
                this.database.ReturnToPool(this);
            }

            public void DisposeConnection()
            {
                this.connection.Dispose();
                this.connection = null;
                this.preparedInsert.Dispose();
                this.preparedInsert = null;
            }

            public IDbCommand GetPreparedInsert()
            {
                return this.preparedInsert;
            }

            public void Open()
            {
                this.connection.Open();
            }
        }
    }
}
