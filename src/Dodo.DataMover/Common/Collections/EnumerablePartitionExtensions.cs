using System.Collections.Generic;
using System.Linq;

namespace Dodo.DataMover.Common.Collections
{
    public static class EnumerablePartitionExtensions
    {
        /// <summary>
        /// StatelessPartition splits <paramref name="source"/> to partitions, <paramref name="partitionSize"/> items each.
        /// </summary>
        /// <param name="source">Source.</param>
        /// <param name="partitionSize">Partition size.</param>
        /// <typeparam name="T">Element type.</typeparam>
        /// <returns>Returns partitions in form of <see cref="List{T}"/> of <typeparamref name="T"/>.</returns>
        public static IEnumerable<IReadOnlyList<T>> StatelessPartition<T>(this IEnumerable<T> source, int partitionSize)
        {
            return StatefulPartition(source, partitionSize).Select(stateful => stateful.ToList());
        }

        /// <summary>
        /// StatefulPartition splits <paramref name="source"/> to partitions, <paramref name="partitionSize"/> items each.
        /// </summary>
        /// <param name="source">Source.</param>
        /// <param name="partitionSize">Partition size.</param>
        /// <typeparam name="T">Element type.</typeparam>
        /// <returns>Returns partitions in form of <see cref="IEnumerable{T}"/> of <typeparamref name="T"/>.
        /// Each partition must be completely enumerated before iterating to the next partition.
        /// Partitions can not be enumerated multiple times.
        /// If client needs enumerating partitions multiple times, use <see cref="StatelessPartition{T}"/>.</returns>
        public static IEnumerable<IEnumerable<T>> StatefulPartition<T>(this IEnumerable<T> source, int partitionSize)
        {
            using (var enumerator = source.GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    yield return StatefulTake(enumerator, partitionSize);
                }
            }
        }

        private static IEnumerable<T> StatefulTake<T>(IEnumerator<T> sourceEnumerator, int partitionSize)
        {
            var count = 0;
            do
            {
                count++;
                yield return sourceEnumerator.Current;
            } while (count < partitionSize && sourceEnumerator.MoveNext());
        }
    }
}
