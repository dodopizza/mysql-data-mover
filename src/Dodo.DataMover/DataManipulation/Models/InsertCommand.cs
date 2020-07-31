namespace Dodo.DataMover.DataManipulation.Models
{
    public class InsertCommand
    {
        public string TableName { get; set; }

        public Batch Batch { get; set; }
    }
}
