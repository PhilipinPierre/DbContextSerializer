namespace DbContextSerializer.Model
{
    internal class ClassInclude
    {
        public List<ClassInclude> Child { get; set; }
        public ClassInclude? Parent { get; set; }
        public String TypeName { get; set; }
        public Type TypeType { get; set; }
    }
}