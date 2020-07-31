using System.Collections.Generic;

namespace Dodo.DataMover.DataManipulation.Models
{
    public class ReadCommand
    {
        public TableSchema TableSchema { get; }

        public List<object> FromPrimaryKey { get; }

        public List<object> ToPrimaryKey { get; }

        public ReadCommand(
            TableSchema tableSchema,
            List<object> fromPrimaryKey,
            List<object> toPrimaryKey
        )
        {
            TableSchema = tableSchema;
            FromPrimaryKey = fromPrimaryKey;
            ToPrimaryKey = toPrimaryKey;
        }
    }
}
