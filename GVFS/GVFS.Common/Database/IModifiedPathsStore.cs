using System;
using System.Collections.Generic;
using System.Text;

namespace GVFS.Common.Database
{
    public interface IModifiedPathsStore
    {
        bool TryAdd(string modifiedPath);
        bool TryRemove(string modifiedPath);
        bool TryGetAll(out string[] modifiedPaths);
    }
}
