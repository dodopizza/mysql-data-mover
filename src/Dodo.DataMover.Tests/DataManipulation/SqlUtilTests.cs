using System.Collections.Generic;
using Dodo.DataMover.DataManipulation;
using NUnit.Framework;

namespace Dodo.DataMover.Tests.DataManipulation
{
    public class SqlUtilTests
    {
        [Test]
        public void MakeLexicalLessThanComparision_OnePKColumn_ShouldReturnValidQuery()
        {
            // Arrange
            var columns = new List<string>
            {
                "Id1"
            };
            var parameters = new List<string>
            {
                "p1"
            };

            // Act
            var result = SqlUtil.MakeLexicalLessThanComparision(columns, parameters);

            // Assert
            Assert.AreEqual("`Id1` < @p1", result);
        }

        [Test]
        public void MakeLexicalLessThanComparision_TwoPKColumns_ShouldReturnValidQuery()
        {
            // Arrange
            var columns = new List<string>
            {
                "Id1",
                "Id2"
            };
            var parameters = new List<string>
            {
                "p1",
                "p2"
            };

            // Act
            var result = SqlUtil.MakeLexicalLessThanComparision(columns, parameters);

            // Assert
            Assert.AreEqual("`Id1` < @p1 or (`Id1` = @p1 and (`Id2` < @p2))", result);
        }

        [Test]
        public void MakeLexicalLessThanComparision_FivePKColumns_ShouldReturnValidQuery()
        {
            // Arrange
            var columns = new List<string>
            {
                "Id1",
                "Id2",
                "Id3",
                "Id4",
                "Id5"
            };
            var parameters = new List<string>
            {
                "p1",
                "p2",
                "p3",
                "p4",
                "p5"
            };

            // Act
            var result = SqlUtil.MakeLexicalLessThanComparision(columns, parameters);

            // Assert
            Assert.AreEqual(
                "`Id1` < @p1 or (`Id1` = @p1 and (`Id2` < @p2 or (`Id2` = @p2 and (`Id3` < @p3 or (`Id3` = @p3 and (`Id4` < @p4 or (`Id4` = @p4 and (`Id5` < @p5))))))))",
                result);
        }
    }
}
