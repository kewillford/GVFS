using CommandLine;
using GVFS.Common;
using GVFS.Common.Git;
using GVFS.Common.Http;
using GVFS.Common.Tracing;
using System;
using System.Collections.Generic;
using System.IO;

namespace GVFS.CommandLine
{
    [Verb(RemoveVerb.RemoveVerbName, HelpText = "Remove paths from the sparse-checkout")]
    public class RemoveVerb : GVFSVerb.ForExistingEnlistment
    {
        private const string RemoveVerbName = "remove";
        private JsonTracer tracer;
        private GVFSEnlistment enlistment;
        private string cacheServerUrl;

        [Option(
            "folders",
            Required = true,
            Default = "",
            HelpText = "A semicolon-delimited list of folders to fetch. Wildcards are not supported.")]
        public string Folders { get; set; }

        [Option(
            "verbose",
            Required = false,
            Default = true,
            HelpText = "Show all outputs on the console in addition to writing them to a log file.")]
        public bool Verbose { get; set; }

        protected override string VerbName => RemoveVerb.RemoveVerbName;

        protected override void Execute(GVFSEnlistment enlistment)
        {
            this.enlistment = enlistment;

            try
            {
                this.tracer = new JsonTracer(GVFSConstants.GVFSEtwProviderName, "Remove");
                this.tracer.AddLogFileEventListener(
                    GVFSEnlistment.GetNewGVFSLogFileName(enlistment.GVFSLogsRoot, GVFSConstants.LogFileTypes.Remove),
                    EventLevel.Informational,
                    Keywords.Any);

                this.cacheServerUrl = CacheServerResolver.GetUrlFromConfig(enlistment);
                this.tracer.WriteStartEvent(
                    enlistment.EnlistmentRoot,
                    enlistment.RepoUrl,
                    this.cacheServerUrl);

                string error;
                ModifiedPathsDatabase modifiedPaths;
                if (!ModifiedPathsDatabase.TryLoadOrCreate(
                    this.tracer,
                    Path.Combine(this.enlistment.DotGVFSRoot, GVFSConstants.DotGVFS.Databases.ModifiedPaths),
                    new Common.FileSystem.PhysicalFileSystem(),
                    out modifiedPaths,
                    out error))
                {
                    throw new InvalidRepoException(error);
                }

                using (modifiedPaths)
                {
                    foreach (string folder in this.Folders.Split(';'))
                    {
                        string lineToRemove;
                        if (!folder.StartsWith("/"))
                        {
                            lineToRemove = "/" + folder;
                        }
                        else
                        {
                            lineToRemove = folder;
                        }

                        modifiedPaths.TryRemove(lineToRemove, isFolder: true, isRetryable: out bool isRetryable);
                    }
                }

                if (!this.Verbose)
                {
                    this.ResetIndex();
                    ////this.UpdateSparseCheckout();
                    ////this.DeleteFromWorkingDirectory();
                }
                else
                {
                    this.ShowStatusWhileRunning(this.ResetIndex, "Updating index");
                    ////this.ShowStatusWhileRunning(this.UpdateSparseCheckout, "Updating sparse-checkout file");
                    ////this.ShowStatusWhileRunning(this.DeleteFromWorkingDirectory, "Removing paths from the working directory");
                }
            }
            catch (Exception e)
            {
                this.tracer.RelatedError(e.Message);
            }
            finally
            {
                this.tracer.Dispose();
            }
        }

        ////private bool UpdateSparseCheckout()
        ////{
        ////    string sparseCheckoutPath = Path.Combine(this.enlistment.WorkingDirectoryBackingRoot, GVFSConstants.DotGit.Info.SparseCheckoutPath);
        ////    SortedSet<string> sparseCheckout = new SortedSet<string>();

        ////    foreach (string line in File.ReadAllText(sparseCheckoutPath).Split('\n'))
        ////    {
        ////        if (!line.StartsWith("/*")
        ////            && !line.StartsWith("!/")
        ////            && !string.IsNullOrEmpty(line)
        ////            && !this.foldersToRemove.Contains(line))
        ////        {
        ////            sparseCheckout.Add(line);
        ////        }
        ////    }

        ////    using (FileStream outStream = File.OpenWrite(sparseCheckoutPath))
        ////    using (StreamWriter writer = new StreamWriter(outStream))
        ////    {
        ////        foreach (string line in sparseCheckout)
        ////        {
        ////            writer.Write(line + "\n");
        ////        }
        ////    }

        ////    return true;
        ////}

        private bool ResetIndex()
        {
            GitProcess git = new GitProcess(this.enlistment);
            GitProcess.Result result = git.ResetHeadToSparseCheckout();

            if (result.ExitCodeIsFailure)
            {
                this.tracer.RelatedError($"Failed to reset index to HEAD: %s", result.Errors);
            }

            return true;
        }

        ////private bool DeleteFromWorkingDirectory()
        ////{
        ////    foreach (string folder in this.foldersToRemove)
        ////    {
        ////        string localPath = folder;

        ////        if (folder.StartsWith("/"))
        ////        {
        ////            localPath = folder.Substring(1);
        ////        }

        ////        string path = Path.Combine(this.enlistment.WorkingDirectoryBackingRoot, localPath);
        ////        GVFSPlatform.Instance.FileSystem.TryGetNormalizedPath(path, out string finalPath, out string message);
        ////        Directory.Delete(finalPath, recursive: true);
        ////    }

        ////    return true;
        ////}
    }
}
