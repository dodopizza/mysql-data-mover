using System.Collections.Generic;

namespace Dodo.DataMover.DataManipulation.Models
{
    public class Batch
    {
        public List<Column> Columns { get; set; }
        public List<List<object>> Rows { get; set; }
    }
}
