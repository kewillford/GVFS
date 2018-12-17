using GVFS.Common;
using GVFS.Tests.Should;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

namespace GVFS.UnitTests.Common
{
    [TestFixture]
    public class ConsoleSpinnerTests
    {
        [TestCase]
        public void Test()
        {
            using (TestConsoleOutput output = new TestConsoleOutput())
            {
                using (ConsoleSpinner spinner = new ConsoleSpinner(output, "Testing"))
                {
                    spinner.WriteMessage("Working at #1");
                    Thread.Sleep(1000);
                    spinner.WriteResult(true);
                    spinner.WriteMessage("Working at #2");
                    Thread.Sleep(1000);
                    spinner.WriteResult(false);
                }

                output.OutputHistory.ToString().ShouldEqual("testing...Succeeded" + Environment.NewLine);
            }
        }

        public class TestConsoleOutput : ConsoleSpinner.IOutputWriter, IDisposable
        {
            private StringWriter stringWriter;

            public TestConsoleOutput()
            {
                this.OutputHistory = new StringBuilder();
                this.stringWriter = new StringWriter(this.OutputHistory);
            }

            public StringBuilder OutputHistory { get; }

            public void Dispose()
            {
                this.stringWriter.Dispose();
                this.stringWriter = null;
            }

            public void Write(string value, int removeLength = 0)
            {
                if (removeLength > 0)
                {
                    int index = this.OutputHistory.Length - removeLength;
                    if (index < 0)
                    {
                        index = 0;
                    }

                    int length = removeLength;
                    if (length > this.OutputHistory.Length)
                    {
                        length = this.OutputHistory.Length;
                    }

                    this.OutputHistory.Remove(index, length);
                }

                this.stringWriter.Write(value);
            }
        }
    }
}
