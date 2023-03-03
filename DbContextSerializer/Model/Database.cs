namespace DbContextSerializer.Model
{
    internal class Database
    {
        public string ContextName { get; set; }
        public Type ContextType { get; set; }
        public string Name { get; set; }
        public Server Server { get; set; }

        public List<Table> Tables { get; set; }
    }
}