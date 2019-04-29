using System;
using System.Collections.Generic;
using System.Text;

namespace GVFS.Common
{
    public interface IPlaceholder
    {
        string Path { get; }
        string Sha { get; }
        bool IsFolder { get; }
        bool IsExpandedFolder { get; }
        bool IsPossibleTombstoneFolder { get; }
    }
}
