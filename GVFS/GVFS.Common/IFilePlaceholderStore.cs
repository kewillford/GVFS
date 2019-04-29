using System;
using System.Collections.Generic;
using System.Text;

namespace GVFS.Common
{
    public interface IFilePlaceholderStore
    {
        void AddFilePlaceholder(string path, string sha);
        void RemoveFilePlaceholder(string path);
    }
}
