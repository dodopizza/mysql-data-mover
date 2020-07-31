namespace Dodo.DataMover.DataManipulation.DatabaseMapping
{
    public class ColumnDto
    {
        public string TableName { get; set; }

        public string Name { get; set; }

        public int? PkOrdinalPosition { get; set; }

        public string ColumnType { get; set; }

        public string DataType { get; set; }
    }
}
