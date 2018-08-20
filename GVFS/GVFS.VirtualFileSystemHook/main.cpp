#include "stdafx.h"
#include "common.h"

enum VirtualFileSystemErrorReturnCode
{
	ErrorVirtualFileSystemProtocol = ReturnCode::LastError + 1,
};

int main(int argc, char *argv[])
{
    if (argc != 2)
    {
        die(VirtualFileSystemErrorReturnCode::ErrorVirtualFileSystemProtocol, "Invalid arguments");
    }

    if (strcmp(argv[1], "1"))
    {
        die(VirtualFileSystemErrorReturnCode::ErrorVirtualFileSystemProtocol, "Bad version");
    }

    DisableCRLFTranslationOnStdPipes();

    PATH_STRING pipeName(GetGVFSPipeName(argv[0]));
    PIPE_HANDLE pipeHandle = CreatePipeToGVFS(pipeName);

    // Construct projection request message
    unsigned long bytesWritten;
    unsigned long messageLength = 6;
    int error = 0;
    bool success = WriteToPipe(
        pipeHandle,
        "MPL|1\n",
        messageLength,
        &bytesWritten,
        &error);

    if (!success || bytesWritten != messageLength)
    {
        die(ReturnCode::PipeWriteFailed, "Failed to write to pipe (%d)\n", error);
    }

    char message[1024];
    unsigned long bytesRead;
    int lastError;
    bool finishedReading = false;
    bool firstRead = true;
    do
    {
        char *pMessage = &message[0];

        success = ReadFromPipe(
            pipeHandle,
            message,
            sizeof(message),
            &bytesRead,
            &lastError);

        if (!success)
        {
            break;
        }
        
        messageLength = bytesRead;

        if (firstRead)
        {
            firstRead = false;
            if (message[0] != 'S')
            {
                die(ReturnCode::PipeReadFailed, "Read response from pipe failed (%s)\n", message);
            }

            pMessage += 2;
            messageLength -= 2;
        }

        if (*(pMessage + messageLength - 1) == '\n')
        {
            finishedReading = true;
            messageLength -= 1;
        }

        fwrite(pMessage, 1, messageLength, stdout);

    } while (success && !finishedReading);

    if (!success)
    {
        die(ReturnCode::PipeReadFailed, "Read response from pipe failed (%d)\n", lastError);
    }

    return 0;
}

