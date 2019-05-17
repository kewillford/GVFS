using GVFS.Common.Database;
using GVFS.Tests.Should;
using Moq;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Data;
using System.Text;

namespace GVFS.UnitTests.Common.Database
{
    [TestFixture]
    public class PlaceholdersTests
    {
        [TestCase]
        public void ConstructorTest()
        {
            Mock<IGVFSConnectionPool> mockConnectionPool = new Mock<IGVFSConnectionPool>(MockBehavior.Strict);
            Placeholders placeholders = new Placeholders(mockConnectionPool.Object);
            mockConnectionPool.VerifyAll();
        }

        [TestCase]
        public void CreateTableTest()
        {
            Mock<IDbCommand> mockCommand = new Mock<IDbCommand>(MockBehavior.Strict);
            mockCommand.SetupSet(x => x.CommandText = "CREATE TABLE IF NOT EXISTS [Placeholders] (path TEXT PRIMARY KEY, pathType TINYINT NOT NULL, sha char(40) ) WITHOUT ROWID;");
            mockCommand.Setup(x => x.ExecuteNonQuery()).Returns(1);
            Placeholders.CreateTable(mockCommand.Object);
            mockCommand.VerifyAll();
        }

        [TestCase]
        public void CountTest()
        {
            this.TestPlaceholders(
                (placeholders, mockCommand) =>
                {
                    mockCommand.SetupSet(x => x.CommandText = "SELECT count(path) FROM Placeholders;");
                    mockCommand.Setup(x => x.ExecuteScalar()).Returns(0);
                    placeholders.Count().ShouldEqual(0);
                });
        }

        [TestCase]
        public void GetAllFilePathsWithNoResults()
        {
            this.TestPlaceholdersWithReader(
               (placeholders, mockCommand, mockReader) =>
               {
                   mockReader.Setup(x => x.Read()).Returns(false);
                   mockCommand.SetupSet(x => x.CommandText = "SELECT path FROM Placeholders WHERE pathType = 0;");

                   HashSet<string> filePaths = placeholders.GetAllFilePaths();
                   filePaths.ShouldNotBeNull();
                   filePaths.Count.ShouldEqual(0);
               });
        }

        [TestCase]
        public void GetAllFilePathsTest()
        {
            this.TestPlaceholdersWithReader(
               (placeholders, mockCommand, mockReader) =>
               {
                   mockReader.Setup(x => x.Read()).Returns(true);
                   mockReader.Setup(x => x.GetString(0)).Returns("test");
                   mockCommand.SetupSet(x => x.CommandText = "SELECT path FROM Placeholders WHERE pathType = 0;");

                   HashSet<string> filePaths = placeholders.GetAllFilePaths();
                   filePaths.ShouldNotBeNull();
                   filePaths.Count.ShouldEqual(1);
                   filePaths.ShouldContain(x => x == "test");
               });
        }

        private void TestPlaceholdersWithReader(Action<Placeholders, Mock<IDbCommand>, Mock<IDataReader>> testCode)
        {
            this.TestPlaceholders(
                (placeholders, mockCommand) =>
                {
                    Mock<IDataReader> mockReader = new Mock<IDataReader>(MockBehavior.Strict);
                    mockReader.Setup(x => x.Dispose());

                    mockCommand.Setup(x => x.ExecuteReader()).Returns(mockReader.Object);
                    testCode(placeholders, mockCommand, mockReader);
                    mockReader.VerifyAll();
                });
        }

        private void TestPlaceholders(Action<Placeholders, Mock<IDbCommand>> testCode)
        {
            Mock<IDbCommand> mockCommand = new Mock<IDbCommand>(MockBehavior.Strict);
            mockCommand.Setup(x => x.Dispose());

            Mock<IDbConnection> mockConnection = new Mock<IDbConnection>(MockBehavior.Strict);
            mockConnection.Setup(x => x.CreateCommand()).Returns(mockCommand.Object);

            Mock<IPooledConnection> mockPooledConnection = new Mock<IPooledConnection>(MockBehavior.Strict);
            mockPooledConnection.SetupGet(x => x.Connection).Returns(mockConnection.Object);
            mockPooledConnection.Setup(x => x.Dispose());

            Mock<IGVFSConnectionPool> mockConnectionPool = new Mock<IGVFSConnectionPool>(MockBehavior.Strict);
            mockConnectionPool.Setup(x => x.GetConnection()).Returns(mockPooledConnection.Object);

            Placeholders placeholders = new Placeholders(mockConnectionPool.Object);
            testCode(placeholders, mockCommand);

            mockCommand.VerifyAll();
            mockConnection.VerifyAll();
            mockPooledConnection.VerifyAll();
            mockConnectionPool.VerifyAll();
        }
    }
}
