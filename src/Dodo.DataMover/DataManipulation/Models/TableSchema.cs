using System.Collections.Generic;

namespace Dodo.DataMover.DataManipulation.Models
{
    public class TableSchema
    {
        public string Name { get; set; }
        public List<Column> Columns { get; set; }
    }
}
