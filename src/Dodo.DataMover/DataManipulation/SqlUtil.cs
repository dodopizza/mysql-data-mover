using System;
using System.Collections.Generic;
using System.Linq;

namespace Dodo.DataMover.DataManipulation
{
    public static class SqlUtil
    {
        public static string MakeLexicalLessThanComparision(
            IReadOnlyList<string> columns,
            IReadOnlyList<string> parameters)
        {
            if (columns.Count != parameters.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(parameters),
                    "Parameters count must be equal to columns count");
            }

            return MakeLexicalLessThanComparisionRecursive(columns.Zip(parameters).ToList(), 0);
        }

        private static string MakeLexicalLessThanComparisionRecursive(
            IReadOnlyList<(string c, string p)> keyParts,
            int baseIndex)
        {
            var highComparision = $"`{keyParts[baseIndex].c}` < @{keyParts[baseIndex].p}";
            if (baseIndex == keyParts.Count - 1)
            {
                return highComparision;
            }

            var highComparisionEquality = $"`{keyParts[baseIndex].c}` = @{keyParts[baseIndex].p}";
            return
                $"{highComparision} or ({highComparisionEquality} and ({MakeLexicalLessThanComparisionRecursive(keyParts, baseIndex + 1)}))";
        }

        public static string MakeLexicalGreaterOrEqualComparision(
            IReadOnlyList<string> columns,
            IReadOnlyList<string> parameters)
        {
            if (columns.Count != parameters.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(parameters),
                    "Parameters count must be equal to columns count");
            }

            return MakeLexicalGreaterOrEqualComparisionRecursive(columns.Zip(parameters).ToList(), 0);
        }

        private static string MakeLexicalGreaterOrEqualComparisionRecursive(
            IReadOnlyList<(string c, string p)> keyParts,
            int baseIndex)
        {
            if (baseIndex == keyParts.Count - 1)
            {
                return $"`{keyParts[baseIndex].c}` >= @{keyParts[baseIndex].p}";
            }

            var highComparision = $"`{keyParts[baseIndex].c}` > @{keyParts[baseIndex].p}";
            var highComparisionEquality = $"`{keyParts[baseIndex].c}` = @{keyParts[baseIndex].p}";

            return
                $"{highComparision} or ({highComparisionEquality} and ({MakeLexicalGreaterOrEqualComparisionRecursive(keyParts, baseIndex + 1)}))";
        }
    }
}
