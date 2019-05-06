using System.IO;

namespace GVFS.Common.FileBasedCollections
{
    public class AddFolderEntry : PlaceholderEvent
    {
        public readonly bool IsExpandedFolder;
        public readonly bool IsTombstoneFolder;

        public AddFolderEntry(string path, bool isExpandedFolder, bool isTombstoneFolder) : base(path)
        {
            this.IsExpandedFolder = isExpandedFolder;
            this.IsTombstoneFolder = isTombstoneFolder;
        }

        public override void Serialize(BinaryWriter writer)
        {
            if (this.IsTombstoneFolder)
            {
                writer.Write(TombstoneFolderPrefix);
            }
            else if (this.IsExpandedFolder)
            {
                writer.Write(ExpandedFolderPrefix);
            }
            else
            {
                writer.Write(PartialFolderPrefix);
            }

            writer.Write(this.Path);
        }
    }
}
