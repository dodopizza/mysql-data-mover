using System;
using System.Linq;
using Dodo.DataMover.DataManipulation;
using NUnit.Framework;

namespace Dodo.DataMover.Tests.DataManipulation
{
    public class SourceSchemaReaderTests
    {
        [TestCase("orders,clients,addresses", "", "", "orders,clients,addresses")]
        [TestCase("orders,orders_archive,clients,addresses", "orders", "", "orders,orders_archive,clients,addresses")]
        [TestCase("orders,clients,addresses", "", "orders", "clients,addresses")]
        [TestCase("orders,clients,addresses", "", ".*", "")]
        [TestCase("client,client_order,order_client,order_client_products", "^.*client$", ".*", "client,order_client")]
        [TestCase("orders,clients,addresses", "orders", ".*", "orders")]
        [TestCase("orders,clients,addresses", ".*", ".*", "orders,clients,addresses")]
        [TestCase("orders,clients,addresses", "orders", "", "orders,clients,addresses")]
        [TestCase("orders,clients,addresses", "orders", "orders", "orders,clients,addresses")]
        [TestCase("orders,clients,addresses", "clients", "addresses", "orders,clients")]
        [TestCase("orders,orders_archive,important_archive", "important_archive", ".*_archive$",
            "orders,important_archive")]
        [TestCase("orders,_supplycomposition_del", "", "_del$", "orders")]
        public void TableCriteriaMatchesTests(
            string tableNamesString,
            string includeTableRegexesString,
            string excludeTableRegexesString,
            string expectedString)
        {
            // Arrange
            var tableNames = tableNamesString.Split(",", StringSplitOptions.RemoveEmptyEntries);
            var includeTableRegexes = includeTableRegexesString.Split(",", StringSplitOptions.RemoveEmptyEntries);
            var excludeTableRegexes = excludeTableRegexesString.Split(",", StringSplitOptions.RemoveEmptyEntries);

            var expected = expectedString.Split(",", StringSplitOptions.RemoveEmptyEntries);

            // Act
            var actual = tableNames
                .Where(x => SourceSchemaReader.TableCriteriaMatches(x, includeTableRegexes, excludeTableRegexes))
                .ToArray();

            // Assert
            CollectionAssert.AreEqual(expected, actual);
        }

        [TestCase(0L, 0L, 3, "")]
        [TestCase(0L, 1L, 3, "0")]
        [TestCase(5000L, 5000L, 3, "5000")]
        [TestCase(10000L, 5000L, 3, "5000,5000")]
        [TestCase(1000L, 5000L, 3, "1000")]
        [TestCase(5000L, 2501L, 3, "2501,2499")]
        [TestCase(10001L, 5000L, 3, "5000,5000,1")]
        [TestCase(null, 5000L, 3, "5000,5000,5000")]
        public void GetBatchSizes(long? limit, long batchSize, int maxBatchCount, string expectedString)
        {
            // Arrange
            // Act
            var actual = SourceSchemaReader.GetBatchSizes(limit, batchSize).Take(maxBatchCount).ToArray();

            // Assert
            CollectionAssert.AreEqual(
                expectedString.Split(",", StringSplitOptions.RemoveEmptyEntries).Select(long.Parse).ToArray(), actual);
        }
    }
}
