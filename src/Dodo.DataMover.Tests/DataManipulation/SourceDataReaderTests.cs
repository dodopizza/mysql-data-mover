using System.Collections.Generic;
using Dodo.DataMover.DataManipulation;
using Dodo.DataMover.DataManipulation.Models;
using NUnit.Framework;

namespace Dodo.DataMover.Tests.DataManipulation
{
    public class SourceDataReaderTests
    {
        [Test]
        public void BuildQuery_OnePK_ShouldReturnValidQuery()
        {
            // Arrange
            var command = new ReadCommand
            (
                new TableSchema
                {
                    Name = "Orders",
                    Columns = new List<Column>
                    {
                        new Column
                        {
                            Name = "Id",
                            PkOrdinalPosition = 1
                        },
                        new Column
                        {
                            Name = "Name"
                        }
                    }
                },
                new List<object>
                {
                    1
                },
                new List<object>
                {
                    10
                }
            );

            var columns = new List<Column>
            {
                new Column
                {
                    Name = "Id",
                    PkOrdinalPosition = 1
                },
                new Column
                {
                    Name = "Name"
                }
            };

            // Act
            var result = SourceDataReader.BuildQuery(command, columns);

            // Assert
            Assert.AreEqual("select `Id`,`Name` from `Orders` where (`Id` >= @from_0) and (`Id` < @to_0)", result);
        }

        [Test]
        public void BuildQuery_TwoPK_ShouldReturnValidQuery()
        {
            // Arrange
            var command = new ReadCommand
            (
                new TableSchema
                {
                    Name = "Orders",
                    Columns = new List<Column>
                    {
                        new Column
                        {
                            Name = "Id0",
                            PkOrdinalPosition = 1
                        },
                        new Column
                        {
                            Name = "Id1",
                            PkOrdinalPosition = 2
                        },
                        new Column
                        {
                            Name = "Name"
                        }
                    }
                },
                new List<object>
                {
                    1,
                    1
                },
                new List<object>
                {
                    10,
                    10
                }
            );

            var columns = new List<Column>
            {
                new Column
                {
                    Name = "Id0",
                    PkOrdinalPosition = 1
                },
                new Column
                {
                    Name = "Id1",
                    PkOrdinalPosition = 2
                },
                new Column
                {
                    Name = "Name"
                }
            };

            // Act
            var result = SourceDataReader.BuildQuery(command, columns);

            // Assert
            Assert.AreEqual(
                "select `Id0`,`Id1`,`Name` from `Orders` where (`Id0` > @from_0 or (`Id0` = @from_0 and (`Id1` >= @from_1))) and (`Id0` < @to_0 or (`Id0` = @to_0 and (`Id1` < @to_1)))",
                result);
        }
    }
}
