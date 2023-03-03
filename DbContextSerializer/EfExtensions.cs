using DbContextSerializer.Model;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.Collections;
using System.Reflection;

namespace DbContextSerializer
{
    public static class EfExtensions
    {
        public static async Task<TContext>? DeSerializeDbContext<TContext>(string path) where TContext : DbContext
        {
            string fullPath = string.Empty;
            if (File.Exists(path))
            {
                fullPath = path;
            }
            else if (Directory.Exists(path) && new DirectoryInfo(path) is DirectoryInfo directoryInfo)
            {
                foreach (FileInfo file in directoryInfo.EnumerateFiles(nameof(TContext) + ".json"))
                {
                    fullPath = file.FullName;
                }
            }
            if (!string.IsNullOrEmpty(fullPath) && Newtonsoft.Json.JsonConvert.DeserializeObject(File.ReadAllText(path)) is object contextObj && contextObj != null && contextObj is TContext context)
            {
                return context;
            }
            return null;
        }

        public static List<PropertyInfo> GetDbSetProperties(this DbContext context)
        {
            var dbSetProperties = new List<PropertyInfo>();
            var properties = context.GetType().GetProperties();

            foreach (var property in properties)
            {
                var setType = property.PropertyType;

                //#if EF5 || EF6
                //            var isDbSet = setType.IsGenericType && (typeof (IDbSet<>).IsAssignableFrom(setType.GetGenericTypeDefinition()) || setType.GetInterface(typeof (IDbSet<>).FullName) != null);
                //#elif EF7
                var isDbSet = setType.IsGenericType && (typeof(DbSet<>).IsAssignableFrom(setType.GetGenericTypeDefinition()));
                //#endif

                if (isDbSet)
                {
                    dbSetProperties.Add(property);
                }
            }

            return dbSetProperties;
        }

        public static bool IsIenumerable(this Type type)
        {
            return typeof(IEnumerable).IsAssignableFrom(type);
        }

        public static async Task SerializeDbContext<TContext>(this DbContext dbContext, string path, bool showInConsole = false) where TContext : DbContext
        {
            var connection = dbContext.Database.GetDbConnection();
            Server server = new Server() { Name = connection.DataSource, Databases = new List<Database>() };
            List<List<object>> dbSets = new List<List<object>>();
            var lines = new List<string[]>();
            var tables = dbContext.Model.GetEntityTypes()
            .Distinct()
            .ToList();

            Database database = new Database()
            {
                Name = connection.Database,
                ContextName = nameof(TContext),
                Server = server,
                ContextType = typeof(TContext),
                Tables = new List<Table>()
            };
            server.Databases.Add(database);
            foreach (var entityTable in tables)
            {
                bool isTable = false, isView = false;
                string? tableName = entityTable.GetTableName();
                if (tableName == null)
                {
                    tableName = entityTable.GetViewName();
                    if (tableName == null)
                    {
                        throw new NotImplementedException();
                    }
                    else
                    {
                        isView = true;
                    }
                }
                else
                {
                    isTable = true;
                }
                Table table = new Table()
                {
                    Name = tableName,
                    Database = database,
                    ModelName = entityTable.Name,
                    Type = isTable ? TypeTable.Table : isView ? TypeTable.View : TypeTable.Unknown,
                    ModelType = entityTable.GetType(),
                    Columns = new List<Column>(),
                };
                database.Tables.Add(table);

                lines.Add(new[] { server.Name, database.Name, database.ContextName, isTable ? "Table" : isView ? "View" : "Unknown", table.Name, table.ModelName, "", "" });

                foreach (var property in entityTable.GetProperties())
                {
                    Column column = new Column()
                    {
                        Name = property.GetColumnName(),
                        ModelName = property.Name,
                        ModelType = property.DeclaringType.GetType(),
                        Table = table,
                    };
                    table.Columns.Add(column);
                    lines.Add(new[] { "", "", "", "", "", "", column.Name, column.ModelName });
                }
            }

            List<PropertyInfo> dbSetProperties = dbContext.GetDbSetProperties();

            dbSets = dbSetProperties.Select(x => ((IQueryable<object>)x.GetValue(dbContext, BindingFlags.Default, binder: null, index: null, culture: null)).ToList()).ToList();
            int i = 0;
            await File.WriteAllTextAsync(
                                    "c:\\Temp\\" + server.Name + "_" + server.Databases[0].Name + ".json",
                                    Newtonsoft.Json.JsonConvert.SerializeObject(dbSets, Formatting.Indented, new JsonSerializerSettings
                                    {
                                        //ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                                        PreserveReferencesHandling = PreserveReferencesHandling.Objects,
                                        MaxDepth = 1
                                    }
                                    ));
            await File.WriteAllTextAsync(
                                    "c:\\Temp\\" + dbContext.GetType().Name + ".json",
                                    Newtonsoft.Json.JsonConvert.SerializeObject(dbSets, Formatting.Indented, new JsonSerializerSettings
                                    {
                                        //ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                                        PreserveReferencesHandling = PreserveReferencesHandling.Objects,
                                        MaxDepth = 1
                                    }
                                    ));
            foreach (var dbSet in dbSets)
            {
                //var list = dbSet.Pro
                var type = dbSet.GetType();
                try
                {
                    await File.WriteAllTextAsync(
                                    "c:\\Temp\\" + tables[i].DisplayName() + ".json",
                                    Newtonsoft.Json.JsonConvert.SerializeObject(dbSet, Formatting.Indented, new JsonSerializerSettings
                                    {
                                        //ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                                        PreserveReferencesHandling = PreserveReferencesHandling.Objects,
                                        MaxDepth = 1
                                    }
                                    ));
                }
                catch (Exception ex)
                {
                    if (showInConsole)
                        Console.WriteLine(ex.ToString());
                }

                //using (TextWriter writer = File.CreateText(tables[i].DisplayName() + ".json"))
                //{
                //    var serializer = new JsonSerializer() { MaxDepth = 3, For };
                //    serializer.Serialize(writer, dbSet);
                //}
                i++;
            }
            if (showInConsole)
                Console.WriteLine(ConsoleUtility.PadElementsInLines(lines, 3));
        }

        internal static void RecursiveIncludes(ref List<String> Includes, ClassInclude include, string baseString)
        {
            string newString = (string.IsNullOrEmpty(baseString) ? "" : baseString + ".") + include.TypeName;
            Includes.Add(newString);
            foreach (ClassInclude classInclude in include.Child)
            {
                RecursiveIncludes(ref Includes, classInclude, newString);
            }
        }

        private static void GetIncludeTypes(this ClassInclude? parent, ref List<ClassInclude> includes, Type type, string baseNamespace, ref HashSet<Type> ignoreSubTypes, bool addSeenTypesToIgnoreList = true, int maxDepth = int.MaxValue)
        {
            try
            {
                var properties = type.GetProperties();
                foreach (var property in properties.Where(p => p.PropertyType.FullName != null && (!p.PropertyType.FullName.StartsWith("System.String") && p.PropertyType.IsIenumerable() || p.PropertyType.FullName.StartsWith(baseNamespace, StringComparison.InvariantCultureIgnoreCase))))
                {
                    if (!ignoreSubTypes.Contains(property.PropertyType))
                    {
                        var getter = property.GetGetMethod();
                        if (getter != null)
                        {
                            var propPath = property.Name;
                            if (maxDepth <= propPath.Count(c => c == '.')) { return; }

                            List<ClassInclude> childs = new List<ClassInclude>();
                            ClassInclude toInclude = new ClassInclude()
                            {
                                TypeName = propPath,
                                TypeType = property.PropertyType,
                                Parent = parent,
                            };

                            includes.Add(toInclude);
                            var subType = property.PropertyType;
                            if (!ignoreSubTypes.Contains(subType) && addSeenTypesToIgnoreList)
                            {
                                // add each type that we have processed to ignore list to prevent recursions
                                ignoreSubTypes.Add(type);
                            }

                            var isEnumerableType = subType.IsIenumerable();
                            var genericArgs = subType.GetGenericArguments();
                            if (isEnumerableType && genericArgs.Length == 1)
                            {
                                // sub property is collection, use collection type and drill down
                                var subTypeCollection = genericArgs[0];
                                if (subTypeCollection != null && !ignoreSubTypes.Contains(subTypeCollection))
                                {
                                    toInclude.GetIncludeTypes(ref childs, subTypeCollection, baseNamespace, ref ignoreSubTypes, addSeenTypesToIgnoreList, maxDepth);
                                }
                            }
                            else if (!ignoreSubTypes.Contains(subType))
                            {
                                // sub property is no collection, drill down directly
                                toInclude.GetIncludeTypes(ref childs, subType, baseNamespace, ref ignoreSubTypes, addSeenTypesToIgnoreList, maxDepth);
                            }
                            toInclude.Child = childs;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }
    }
}