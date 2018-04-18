namespace Generator
{
    public class Index
    {
        public string Schema;
        public string TableName;
        public string IndexName;
        public byte KeyOrdinal;
        public string ColumnName;
        public int ColumnCount;
        public bool IsUnique;
        public bool IsPrimaryKey;
        public bool IsUniqueConstraint;
        public bool IsClustered;
    }
}