namespace GVFS.Common.Database
{
    /// <summary>
    /// Interface for getting a pooled database connection
    /// </summary>
    public interface IGVFSConnectionPool
    {
        GVFSDatabase.IGVFSConnection GetConnection();
    }
}
