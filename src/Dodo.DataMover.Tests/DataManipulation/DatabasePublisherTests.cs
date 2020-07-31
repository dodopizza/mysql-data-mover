using System.Collections.Generic;
using Dodo.DataMover.DataManipulation;
using Dodo.DataMover.DataManipulation.Models;
using NUnit.Framework;

namespace Dodo.DataMover.Tests.DataManipulation
{
    public class DatabasePublisherTests
    {
        [Test]
        public void GenerateSql_PartitionWithOneRow_ShouldReturnValidString()
        {
            // Arrange
            var insertCommand = new InsertCommand
            {
                TableName = "TestTable",
                Batch = new Batch
                {
                    Columns = new List<Column>
                    {
                        new Column {Name = "IdField"},
                        new Column {Name = "DateTimeField"},
                        new Column {Name = "MessageField"}
                    }
                }
            };

            var partitionList = new List<List<object>>
            {
                new List<object>
                {
                    1,
                    "2020-04-10 02:42:09",
                    "Test message"
                },
                new List<object>
                {
                    2,
                    "2020-04-10 02:42:09",
                    "Test message"
                }
            };

            // Act
            var res = DatabasePublisher.GenerateSql(insertCommand.TableName, insertCommand.Batch.Columns,
                partitionList.Count, false);

            // Assert
            Assert.AreEqual(
                "insert into `TestTable` (`IdField`,`DateTimeField`,`MessageField`) values (@p1,@p2,@p3),(@p4,@p5,@p6)",
                res);
        }
    }
}
