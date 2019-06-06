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
    [Verb(AddVerb.AddVerbName, HelpText = "Add a path to the sparse-checkout")]
    public class AddVerb : GVFSVerb.ForExistingEnlistment
    {
        private const string AddVerbName = "add";
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

        protected override string VerbName => AddVerb.AddVerbName;

        protected override void Execute(GVFSEnlistment enlistment)
        {
            this.enlistment = enlistment;

            try
            {
                this.tracer = new JsonTracer(GVFSConstants.GVFSEtwProviderName, "Add");
                this.tracer.AddLogFileEventListener(
                    GVFSEnlistment.GetNewGVFSLogFileName(enlistment.GVFSLogsRoot, GVFSConstants.LogFileTypes.Add),
                    EventLevel.Informational,
                    Keywords.Any);

                this.cacheServerUrl = CacheServerResolver.GetUrlFromConfig(enlistment);
                this.tracer.WriteStartEvent(
                    enlistment.EnlistmentRoot,
                    enlistment.RepoUrl,
                    this.cacheServerUrl);

                if (this.Verbose)
                {
                    this.UpdateSparseCheckout();
                    this.PrefetchBlobs();
                    this.ResetIndex();
                }
                else
                {
                    this.ShowStatusWhileRunning(this.PrefetchBlobs, "Prefetching necessary blobs");
                    this.ShowStatusWhileRunning(this.UpdateSparseCheckout, "Updating sparse-checkout file");
                    this.ShowStatusWhileRunning(this.ResetIndex, "Resetting index and populating the working directory");
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

        private bool UpdateSparseCheckout()
        {
            string sparseCheckoutPath = Path.Combine(this.enlistment.WorkingDirectoryBackingRoot, GVFSConstants.DotGit.Info.SparseCheckoutPath);
            SortedSet<string> sparseCheckout = new SortedSet<string>();

            foreach (string line in File.ReadAllText(sparseCheckoutPath).Split('\n'))
            {
                if (!line.StartsWith("/*") && !line.StartsWith("!/") && !string.IsNullOrEmpty(line))
                {
                    sparseCheckout.Add(line);
                }
            }

            foreach (string folder in this.Folders.Split(';'))
            {
                sparseCheckout.Add(folder);
            }

            List<string> finalLines = new List<string>();

            // First line: Add everything
            finalLines.Add("/*");

            // Second line: Don't add anything inside folders
            finalLines.Add("!/*/*");

            // The rest: Add the folders we care about!
            foreach (string folder in sparseCheckout)
            {
                string lineToWrite;
                if (!folder.StartsWith("/"))
                {
                    lineToWrite = "/" + folder;
                }
                else
                {
                    lineToWrite = folder;
                }

                finalLines.Add(lineToWrite);
            }

            using (FileStream outStream = File.OpenWrite(sparseCheckoutPath))
            using (StreamWriter writer = new StreamWriter(outStream))
            {
                foreach (string line in finalLines)
                {
                    writer.Write(line + "\n");
                }
            }

            return true;
        }

        private bool PrefetchBlobs()
        {
            PrefetchVerb prefetchVerb = new PrefetchVerb();
            prefetchVerb.Folders = this.Folders;
            prefetchVerb.Files = string.Empty;
            prefetchVerb.FoldersListFile = string.Empty;
            prefetchVerb.FilesListFile = string.Empty;
            prefetchVerb.Verbose = false;

            prefetchVerb.ExecuteSideBySide(this.enlistment);

            return true;
        }

        private bool ResetIndex()
        {
            GitProcess git = new GitProcess(this.enlistment);
            git.ResetHeadToSparseCheckout();
            return true;
        }
    }
}
