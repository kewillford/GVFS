using System;
using System.Threading;

namespace GVFS.Common
{
    public class ConsoleSpinner : IDisposable
    {
        private static readonly string[] WaitingCharacters = { "\u2014", "\\", "|", "/" };
        private int delayMs = 0;
        private ManualResetEvent actionIsDone = new ManualResetEvent(false);
        private Thread spinnerThread;
        private IOutputWriter output;
        private string message;
        private volatile bool wasMessageWritten = false;
        private string gvfsLogMessage = string.Empty;

        public ConsoleSpinner(IOutputWriter output, string enlistmentRoot, int initialDelayMs = 0)
        {
            if (!string.IsNullOrEmpty(enlistmentRoot))
            {
                this.gvfsLogMessage = $". Run 'gvfs log {enlistmentRoot}' for more info.";
            }

            this.output = output;
            this.delayMs = initialDelayMs;
            this.spinnerThread = new Thread(this.SpinnerThreadProc);
            this.spinnerThread.Start();
        }

        public enum ActionResult
        {
            Success,
            CompletedWithErrors,
            Failure,
        }

        public interface IOutputWriter
        {
            void Write(string value, int removeLength = 0);
        }

        public void WriteMessage(string message)
        {
            this.message = message;
            this.wasMessageWritten = false;
        }

        public void WriteResult(bool succeeded)
        {
            this.WriteResult(succeeded ? ActionResult.Success : ActionResult.Failure);
        }

        public void WriteResult(ActionResult result)
        {
            switch (result)
            {
                case ActionResult.Success:
                    if (this.wasMessageWritten)
                    {
                        this.WriteResult("Succeeded");
                    }

                    break;

                case ActionResult.CompletedWithErrors:
                    if (!this.wasMessageWritten)
                    {
                        this.WriteMessage($"{this.message}...");
                    }

                    this.WriteResult("Completed with errors.");
                    break;

                case ActionResult.Failure:
                    if (!this.wasMessageWritten)
                    {
                        this.WriteMessage($"{this.message}...");
                    }

                    this.WriteResult($"Failed{this.gvfsLogMessage}");
                    break;
            }
        }

        public void Dispose()
        {
            this.actionIsDone.Set();
            this.spinnerThread.Join();
        }

        private void WriteResult(string result)
        {
            this.output.Write(result + Environment.NewLine, removeLength: 1);
        }

        private void SpinnerThreadProc()
        {
            int retries = 0;
            while (!this.actionIsDone.WaitOne(this.delayMs))
            {
                if (!this.wasMessageWritten)
                {
                    this.output.Write($"{this.message}... ");
                    this.wasMessageWritten = true;
                }

                this.output.Write(WaitingCharacters[(retries / 2) % WaitingCharacters.Length], removeLength: 1);

                this.delayMs = 100;
                ++retries;
            }
        }
    }
}
