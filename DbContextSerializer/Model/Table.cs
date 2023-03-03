namespace DbContextSerializer.Model
{
    public enum TypeTable { Table, View, Unknown }

    internal class Table
    {
        public List<Column> Columns { get; set; }
        public Database Database { get; set; }
        public string ModelName { get; set; }
        public Type ModelType { get; set; }
        public string Name { get; set; }

        public TypeTable Type { get; set; }
    }
}