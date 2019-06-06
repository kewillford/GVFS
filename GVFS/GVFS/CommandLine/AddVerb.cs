using CommandLine;
using GVFS.Common;
using GVFS.Common.Git;
using GVFS.Common.Http;
using GVFS.Common.Tracing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GVFS.CommandLine
{
    [Verb(AddVerb.AddVerbName, HelpText = "Add paths to the sparse-checkout")]
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

                this.PrefetchBlobs();

                if (!this.Verbose)
                {
                    this.UpdateSparseCheckout();
                    this.ResetIndex();
                }
                else
                {
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
            GitProcess gitProcess = new GitProcess(this.enlistment);
            GitProcess.Result result = gitProcess.RevParse(GVFSConstants.DotGit.HeadName);
            if (result.ExitCodeIsFailure)
            {
                this.ReportErrorAndExit(this.tracer, result.Errors);
            }

            string headCommitId = result.Output.Trim();

            this.InitializeServerConnection(
                this.tracer,
                this.enlistment,
                this.cacheServerUrl,
                out GitObjectsHttpRequestor objectRequestor,
                out CacheServerInfo cacheServer);

            this.PrefetchBlobs(
                this.tracer,
                this.enlistment,
                headCommitId,
                filesList: null,
                foldersList: this.Folders.Split(';').ToList(),
                lastPrefetchArgs: null,
                objectRequestor: objectRequestor,
                cacheServer: cacheServer,
                verbose: this.Verbose,
                hydrateFiles: false);

            return true;
        }

        private bool ResetIndex()
        {
            GitProcess git = new GitProcess(this.enlistment);
            GitProcess.Result result = git.ResetHeadToSparseCheckout();

            if (result.ExitCodeIsFailure)
            {
                this.tracer.RelatedError($"Failed to reset index to HEAD: %s", result.Errors);
                return false;
            }

            return true;
        }
    }
}
