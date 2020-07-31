namespace Dodo.DataMover.DataManipulation.Models
{
    public class Column
    {
        public string Name { get; set; }

        public int? PkOrdinalPosition { get; set; }
        public string DataType { get; set; }
        public string ColumnType { get; set; }
    }
}
