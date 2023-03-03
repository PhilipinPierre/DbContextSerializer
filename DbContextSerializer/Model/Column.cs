namespace DbContextSerializer.Model
{
    internal class Column
    {
        public string ModelName { get; set; }
        public Type ModelType { get; set; }
        public string Name { get; set; }
        public Table Table { get; set; }
    }
}