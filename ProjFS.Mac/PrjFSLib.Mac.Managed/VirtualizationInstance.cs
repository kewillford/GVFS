﻿using System;
using System.Runtime.InteropServices;

namespace PrjFSLib.Mac
{
    public class VirtualizationInstance
    {
        public const int PlaceholderIdLength = Interop.PrjFSLib.PlaceholderIdLength;

        // We must hold a reference to the delegate to prevent garbage collection
        private NotifyOperationCallback preventGCOnNotifyOperationDelegate;

        // References held to these delegates via class properties
        public virtual EnumerateDirectoryCallback OnEnumerateDirectory { get; set; }
        public virtual GetFileStreamCallback OnGetFileStream { get; set; }
        public virtual LogErrorCallback OnLogError { get; set; }
        public virtual LogWarningCallback OnLogWarning { get; set; }
        public virtual LogInfoCallback OnLogInfo { get; set; }

        public virtual NotifyFileModified OnFileModified { get; set; }
        public virtual NotifyFilePreConvertToFullEvent OnFilePreConvertToFull { get; set; }
        public virtual NotifyPreDeleteEvent OnPreDelete { get; set; }
        public virtual NotifyNewFileCreatedEvent OnNewFileCreated { get; set; }
        public virtual NotifyFileRenamedEvent OnFileRenamed { get; set; }
        public virtual NotifyHardLinkCreatedEvent OnHardLinkCreated { get; set; }

        public static Result ConvertDirectoryToVirtualizationRoot(string fullPath)
        {
            return Interop.PrjFSLib.ConvertDirectoryToVirtualizationRoot(fullPath);
        }

        public static Result RemoveRootAttributes(string fullPath)
        {
            return Interop.PrjFSLib.RemoveRootAttributes(fullPath);
        }

        public virtual Result StartVirtualizationInstance(
            string virtualizationRootFullPath,
            uint poolThreadCount)
        {
            Interop.Callbacks callbacks = new Interop.Callbacks
            {
                OnEnumerateDirectory = this.OnEnumerateDirectory,
                OnGetFileStream = this.OnGetFileStream,
                OnNotifyOperation = this.preventGCOnNotifyOperationDelegate = new NotifyOperationCallback(this.OnNotifyOperation),
                OnLogError = this.OnLogError,
                OnLogWarning = this.OnLogWarning,
                OnLogInfo = this.OnLogInfo
            };

            return Interop.PrjFSLib.StartVirtualizationInstance(
                virtualizationRootFullPath,
                callbacks,
                poolThreadCount);
        }

        public virtual Result StopVirtualizationInstance()
        {
            throw new NotImplementedException();
        }

        public virtual Result WriteFileContents(
            IntPtr fileHandle,
            byte[] bytes,
            uint byteCount)
        {
            GCHandle bytesHandle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            try
            {
                return Interop.PrjFSLib.WriteFileContents(
                    fileHandle,
                    bytesHandle.AddrOfPinnedObject(),
                    byteCount);
            }
            finally
            {
                bytesHandle.Free();
            }
        }

        public virtual Result DeleteFile(
            string relativePath,
            UpdateType updateFlags,
            out UpdateFailureCause failureCause)
        {
            UpdateFailureCause deleteFailureCause = UpdateFailureCause.NoFailure;
            Result result = Interop.PrjFSLib.DeleteFile(
                relativePath,
                updateFlags,
                ref deleteFailureCause);

            failureCause = deleteFailureCause;
            return result;
        }

        public virtual Result WritePlaceholderDirectory(
            string relativePath)
        {
            return Interop.PrjFSLib.WritePlaceholderDirectory(relativePath);
        }

        public virtual Result WritePlaceholderFile(
            string relativePath,
            byte[] providerId,
            byte[] contentId,
            ushort fileMode)
        {
            if (providerId.Length != Interop.PrjFSLib.PlaceholderIdLength ||
                contentId.Length != Interop.PrjFSLib.PlaceholderIdLength)
            {
                throw new ArgumentException();
            }

            return Interop.PrjFSLib.WritePlaceholderFile(
                relativePath,
                providerId,
                contentId,
                fileMode);
        }

        public virtual Result WriteSymLink(
            string relativePath,
            string symLinkTarget)
        {
            return Interop.PrjFSLib.WriteSymLink(relativePath, symLinkTarget);
        }

        public virtual Result UpdatePlaceholderIfNeeded(
            string relativePath,
            byte[] providerId,
            byte[] contentId,
            ushort fileMode,
            UpdateType updateFlags,
            out UpdateFailureCause failureCause)
        {
            if (providerId.Length != Interop.PrjFSLib.PlaceholderIdLength ||
                contentId.Length != Interop.PrjFSLib.PlaceholderIdLength)
            {
                throw new ArgumentException();
            }

            UpdateFailureCause updateFailureCause = UpdateFailureCause.NoFailure;
            Result result = Interop.PrjFSLib.UpdatePlaceholderFileIfNeeded(
                relativePath,
                providerId,
                contentId,
                fileMode,
                updateFlags,
                ref updateFailureCause);

            failureCause = updateFailureCause;
            return result;
        }

        public virtual Result ReplacePlaceholderFileWithSymLink(
            string relativePath,
            string symLinkTarget,
            UpdateType updateFlags,
            out UpdateFailureCause failureCause)
        {
            UpdateFailureCause updateFailureCause = UpdateFailureCause.NoFailure;
            Result result = Interop.PrjFSLib.ReplacePlaceholderFileWithSymLink(
                relativePath,
                symLinkTarget,
                updateFlags,
                ref updateFailureCause);

            failureCause = updateFailureCause;
            return result;
        }

        public virtual Result CompleteCommand(
            ulong commandId,
            Result result)
        {
            throw new NotImplementedException();
        }

        public virtual Result ConvertDirectoryToPlaceholder(
            string relativeDirectoryPath)
        {
            throw new NotImplementedException();
        }

        private Result OnNotifyOperation(
            ulong commandId,
            string relativePath,
            string relativeFromPath,
            byte[] providerId,
            byte[] contentId,
            int triggeringProcessId,
            string triggeringProcessName,
            bool isDirectory,
            NotificationType notificationType)
        {
            switch (notificationType)
            {
                case NotificationType.PreDelete:
                case NotificationType.PreDeleteFromRename:
                    return this.OnPreDelete(relativePath, isDirectory);

                case NotificationType.FileModified:
                    this.OnFileModified(relativePath);
                    return Result.Success;

                case NotificationType.NewFileCreated:
                    this.OnNewFileCreated(relativePath, isDirectory);
                    return Result.Success;

                case NotificationType.FileRenamed:
                    this.OnFileRenamed(relativePath, isDirectory);
                    return Result.Success;

                case NotificationType.HardLinkCreated:
                    this.OnHardLinkCreated(relativeFromPath, relativePath);
                    return Result.Success;

                case NotificationType.PreConvertToFull:
                    return this.OnFilePreConvertToFull(relativePath);
            }

            return Result.ENotYetImplemented;
        }
    }
}
