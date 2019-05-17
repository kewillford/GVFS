using GVFS.Common.Database;
using GVFS.Common.Tracing;
using GVFS.UnitTests.Mock.Common;
using GVFS.UnitTests.Mock.FileSystem;
using Moq;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Text;

namespace GVFS.UnitTests.Common.Database
{
    [TestFixture]
    public class GVFSDatabaseTests
    {
        [TestCase]
        public void ConstructorTest()
        {
            MockFileSystem fileSystem = new MockFileSystem(new MockDirectory("GVFSDatabaseTests", null, null));
            GVFSDatabase database = new GVFSDatabase(new MockTracer(), fileSystem, "mock:root");
        }
    }
}
