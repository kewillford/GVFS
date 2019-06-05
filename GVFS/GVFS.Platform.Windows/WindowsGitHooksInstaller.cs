using GVFS.Common;
using System;
using System.IO;

namespace GVFS.Platform.Windows
{
    internal static class WindowsGitHooksInstaller
    {
        private const string HooksConfigContentTemplate =
@"########################################################################
#   Automatically generated file, do not modify.
#   See {0} config setting
########################################################################
{1}";

        public static void CreateHookCommandConfig(GVFSContext context, string hookName, string commandHookPath)
        {
            string targetPath = commandHookPath + GVFSConstants.GitConfig.HooksExtension;

            try
            {
                string configSetting = GVFSConstants.GitConfig.HooksPrefix + hookName;

                string contents = string.Format(HooksConfigContentTemplate, configSetting, string.Empty);
                Exception ex;
                if (!context.FileSystem.TryWriteTempFileAndRename(targetPath, contents, out ex))
                {
                    throw new RetryableException("Error installing " + targetPath, ex);
                }
            }
            catch (IOException io)
            {
                throw new RetryableException("Error installing " + targetPath, io);
            }
        }
    }
}
