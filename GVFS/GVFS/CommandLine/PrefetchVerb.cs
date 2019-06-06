using CommandLine;
using GVFS.Common;
using GVFS.Common.FileSystem;
using GVFS.Common.Git;
using GVFS.Common.Http;
using GVFS.Common.Maintenance;
using GVFS.Common.Prefetch;
using GVFS.Common.Tracing;
using System;
using System.Collections.Generic;
using System.IO;

namespace GVFS.CommandLine
{
    [Verb(PrefetchVerb.PrefetchVerbName, HelpText = "Prefetch remote objects for the current head")]
    public class PrefetchVerb : GVFSVerb.ForExistingEnlistment
    {
        private const string PrefetchVerbName = "prefetch";

        [Option(
            "files",
            Required = false,
            Default = "",
            HelpText = "A semicolon-delimited list of files to fetch. Simple prefix wildcards, e.g. *.txt, are supported.")]
        public string Files { get; set; }

        [Option(
            "folders",
            Required = false,
            Default = "",
            HelpText = "A semicolon-delimited list of folders to fetch. Wildcards are not supported.")]
        public string Folders { get; set; }

        [Option(
            "folders-list",
            Required = false,
            Default = "",
            HelpText = "A file containing line-delimited list of folders to fetch. Wildcards are not supported.")]
        public string FoldersListFile { get; set; }

        [Option(
            "stdin-files-list",
            Required = false,
            Default = false,
            HelpText = "Specify this flag to load file list from stdin. Same format as when loading from file.")]
        public bool FilesFromStdIn { get; set; }

        [Option(
            "stdin-folders-list",
            Required = false,
            Default = false,
            HelpText = "Specify this flag to load folder list from stdin. Same format as when loading from file.")]
        public bool FoldersFromStdIn { get; set; }

        [Option(
            "files-list",
            Required = false,
            Default = "",
            HelpText = "A file containing line-delimited list of files to fetch. Wildcards are supported.")]
        public string FilesListFile { get; set; }

        [Option(
            "hydrate",
            Required = false,
            Default = false,
            HelpText = "Specify this flag to also hydrate files in the working directory.")]
        public bool HydrateFiles { get; set; }

        [Option(
            'c',
            "commits",
            Required = false,
            Default = false,
            HelpText = "Fetch the latest set of commit and tree packs. This option cannot be used with any of the file- or folder-related options.")]
        public bool Commits { get; set; }

        [Option(
            "verbose",
            Required = false,
            Default = false,
            HelpText = "Show all outputs on the console in addition to writing them to a log file.")]
        public bool Verbose { get; set; }

        protected override string VerbName
        {
            get { return PrefetchVerbName; }
        }

        public void ExecuteSideBySide(GVFSEnlistment enlistment)
        {
            this.Execute(enlistment);
        }

        protected override void Execute(GVFSEnlistment enlistment)
        {
            using (JsonTracer tracer = new JsonTracer(GVFSConstants.GVFSEtwProviderName, "Prefetch"))
            {
                if (this.Verbose)
                {
                    tracer.AddDiagnosticConsoleEventListener(EventLevel.Informational, Keywords.Any);
                }

                string cacheServerUrl = CacheServerResolver.GetUrlFromConfig(enlistment);

                tracer.AddLogFileEventListener(
                    GVFSEnlistment.GetNewGVFSLogFileName(enlistment.GVFSLogsRoot, GVFSConstants.LogFileTypes.Prefetch),
                    EventLevel.Informational,
                    Keywords.Any);
                tracer.WriteStartEvent(
                    enlistment.EnlistmentRoot,
                    enlistment.RepoUrl,
                    cacheServerUrl);

                try
                {
                    EventMetadata metadata = new EventMetadata();
                    metadata.Add("Commits", this.Commits);
                    metadata.Add("Files", this.Files);
                    metadata.Add("Folders", this.Folders);
                    metadata.Add("FileListFile", this.FilesListFile);
                    metadata.Add("FoldersListFile", this.FoldersListFile);
                    metadata.Add("FilesFromStdIn", this.FilesFromStdIn);
                    metadata.Add("FoldersFromStdIn", this.FoldersFromStdIn);
                    metadata.Add("HydrateFiles", this.HydrateFiles);
                    tracer.RelatedEvent(EventLevel.Informational, "PerformPrefetch", metadata);

                    if (this.Commits)
                    {
                        if (!string.IsNullOrWhiteSpace(this.Files) ||
                            !string.IsNullOrWhiteSpace(this.Folders) ||
                            !string.IsNullOrWhiteSpace(this.FoldersListFile) ||
                            !string.IsNullOrWhiteSpace(this.FilesListFile) ||
                            this.FilesFromStdIn ||
                            this.FoldersFromStdIn)
                        {
                            this.ReportErrorAndExit(tracer, "You cannot prefetch commits and blobs at the same time.");
                        }

                        if (this.HydrateFiles)
                        {
                            this.ReportErrorAndExit(tracer, "You can only specify --hydrate with --files or --folders");
                        }

                        GitObjectsHttpRequestor objectRequestor;
                        CacheServerInfo cacheServer;
                        this.InitializeServerConnection(
                            tracer,
                            enlistment,
                            cacheServerUrl,
                            out objectRequestor,
                            out cacheServer);
                        this.PrefetchCommits(tracer, enlistment, objectRequestor, cacheServer);
                    }
                    else
                    {
                        string headCommitId;
                        List<string> filesList;
                        List<string> foldersList;
                        FileBasedDictionary<string, string> lastPrefetchArgs;

                        this.LoadBlobPrefetchArgs(tracer, enlistment, out headCommitId, out filesList, out foldersList, out lastPrefetchArgs);

                        if (BlobPrefetcher.IsNoopPrefetch(tracer, lastPrefetchArgs, headCommitId, filesList, foldersList, this.HydrateFiles))
                        {
                            Console.WriteLine("All requested files are already available. Nothing new to prefetch.");
                        }
                        else
                        {
                            GitObjectsHttpRequestor objectRequestor;
                            CacheServerInfo cacheServer;
                            this.InitializeServerConnection(
                                tracer,
                                enlistment,
                                cacheServerUrl,
                                out objectRequestor,
                                out cacheServer);

                            if (this.HydrateFiles)
                            {
                                if (!this.CheckIsMounted(verbose: true))
                                {
                                    this.ReportErrorAndExit("You can only specify --hydrate if the repo is mounted. Run 'gvfs mount' and try again.");
                                }
                            }

                            this.PrefetchBlobs(tracer, enlistment, headCommitId, filesList, foldersList, lastPrefetchArgs, objectRequestor, cacheServer, this.Verbose, this.HydrateFiles);
                        }
                    }
                }
                catch (VerbAbortedException)
                {
                    throw;
                }
                catch (AggregateException aggregateException)
                {
                    this.Output.WriteLine(
                        "Cannot prefetch {0}. " + ConsoleHelper.GetGVFSLogMessage(enlistment.EnlistmentRoot),
                        enlistment.EnlistmentRoot);
                    foreach (Exception innerException in aggregateException.Flatten().InnerExceptions)
                    {
                        tracer.RelatedError(
                            new EventMetadata
                            {
                                { "Verb", typeof(PrefetchVerb).Name },
                                { "Exception", innerException.ToString() }
                            },
                            $"Unhandled {innerException.GetType().Name}: {innerException.Message}");
                    }

                    Environment.ExitCode = (int)ReturnCode.GenericError;
                }
                catch (Exception e)
                {
                    this.Output.WriteLine(
                        "Cannot prefetch {0}. " + ConsoleHelper.GetGVFSLogMessage(enlistment.EnlistmentRoot),
                        enlistment.EnlistmentRoot);
                    tracer.RelatedError(
                        new EventMetadata
                        {
                            { "Verb", typeof(PrefetchVerb).Name },
                            { "Exception", e.ToString() }
                        },
                        $"Unhandled {e.GetType().Name}: {e.Message}");

                    Environment.ExitCode = (int)ReturnCode.GenericError;
                }
            }
        }

        private void PrefetchCommits(ITracer tracer, GVFSEnlistment enlistment, GitObjectsHttpRequestor objectRequestor, CacheServerInfo cacheServer)
        {
            bool success;
            string error = string.Empty;
            PhysicalFileSystem fileSystem = new PhysicalFileSystem();
            GitRepo repo = new GitRepo(tracer, enlistment, fileSystem);
            GVFSContext context = new GVFSContext(tracer, fileSystem, repo, enlistment);
            GitObjects gitObjects = new GVFSGitObjects(context, objectRequestor);

            if (this.Verbose)
            {
                success = new PrefetchStep(context, gitObjects, requireCacheLock: false).TryPrefetchCommitsAndTrees(out error);
            }
            else
            {
                success = this.ShowStatusWhileRunning(
                    () => new PrefetchStep(context, gitObjects, requireCacheLock: false).TryPrefetchCommitsAndTrees(out error),
                "Fetching commits and trees " + this.GetCacheServerDisplay(cacheServer, enlistment.RepoUrl));
            }

            if (!success)
            {
                this.ReportErrorAndExit(tracer, "Prefetching commits and trees failed: " + error);
            }
        }

        private void LoadBlobPrefetchArgs(
            ITracer tracer,
            GVFSEnlistment enlistment,
            out string headCommitId,
            out List<string> filesList,
            out List<string> foldersList,
            out FileBasedDictionary<string, string> lastPrefetchArgs)
        {
            string error;

            if (!FileBasedDictionary<string, string>.TryCreate(
                    tracer,
                    Path.Combine(enlistment.DotGVFSRoot, "LastBlobPrefetch.dat"),
                    new PhysicalFileSystem(),
                    out lastPrefetchArgs,
                    out error))
            {
                tracer.RelatedWarning("Unable to load last prefetch args: " + error);
            }

            filesList = new List<string>();
            foldersList = new List<string>();

            if (!BlobPrefetcher.TryLoadFileList(enlistment, this.Files, this.FilesListFile, filesList, readListFromStdIn: this.FilesFromStdIn, error: out error))
            {
                this.ReportErrorAndExit(tracer, error);
            }

            if (!BlobPrefetcher.TryLoadFolderList(enlistment, this.Folders, this.FoldersListFile, foldersList, readListFromStdIn: this.FoldersFromStdIn, error: out error))
            {
                this.ReportErrorAndExit(tracer, error);
            }

            GitProcess gitProcess = new GitProcess(enlistment);
            GitProcess.Result result = gitProcess.RevParse(GVFSConstants.DotGit.HeadName);
            if (result.ExitCodeIsFailure)
            {
                this.ReportErrorAndExit(tracer, result.Errors);
            }

            headCommitId = result.Output.Trim();
        }

        private bool CheckIsMounted(bool verbose)
        {
            Func<bool> checkMount = () => this.Execute<StatusVerb>(
                    this.EnlistmentRootPathParameter,
                    verb => verb.Output = new StreamWriter(new MemoryStream())) == ReturnCode.Success;

            if (verbose)
            {
                return ConsoleHelper.ShowStatusWhileRunning(
                    checkMount,
                    "Checking that GVFS is mounted",
                    this.Output,
                    showSpinner: true,
                    gvfsLogEnlistmentRoot: null);
            }
            else
            {
                return checkMount();
            }
        }
    }
}
