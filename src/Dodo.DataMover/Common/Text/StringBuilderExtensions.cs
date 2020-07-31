using System;
using System.Collections.Generic;
using System.Text;

namespace Dodo.DataMover.Common.Text
{
    public static class StringBuilderExtensions
    {
        public static StringBuilder AppendJoin<T>(
            this StringBuilder dst,
            string separator,
            IEnumerable<T> values,
            Action<StringBuilder, T> applyValue) where T : struct
        {
            using var enumerator = values.GetEnumerator();

            if (!enumerator.MoveNext())
            {
                return dst;
            }

            applyValue(dst, enumerator.Current);

            while (enumerator.MoveNext())
            {
                dst.Append(separator);
                applyValue(dst, enumerator.Current);
            }

            return dst;
        }
    }
}
