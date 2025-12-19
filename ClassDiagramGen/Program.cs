using System.Reflection;

if (args.Length < 3)
{
    Console.WriteLine("Usage: ClassDiagramGen <path-to-dll> <namespacePrefix> <outFile.puml>");
    return;
}

var dllPath = Path.GetFullPath(args[0]);
var nsPrefix = args[1];
var outFile = Path.GetFullPath(args[2]);

Assembly asm;
try
{
    asm = Assembly.LoadFrom(dllPath);
}
catch (Exception ex)
{
    Console.WriteLine("Could not load assembly: " + ex.Message);
    return;
}

Type[] types;
try
{
    types = asm.GetTypes();
}
catch (ReflectionTypeLoadException rtle)
{
    types = rtle.Types.Where(t => t != null).Cast<Type>().ToArray();
}

bool InScope(Type t)
    => (t.Namespace ?? "").StartsWith(nsPrefix, StringComparison.Ordinal);

var scopeTypes = types
    .Where(t => t != null)
    .Where(t => InScope(t))
    .Where(t => !t.IsGenericTypeDefinition)
    .OrderBy(t => t.Namespace)
    .ThenBy(t => t.Name)
    .ToList();

string SimpleName(Type t)
{
    if (t.IsNested) return t.Name;
    return t.Name;
}

string FormatType(Type t)
{
    if (t == typeof(string)) return "string";

    if (Nullable.GetUnderlyingType(t) is Type u)
        return FormatType(u) + "?";

    if (t.IsArray)
        return FormatType(t.GetElementType()!) + "[]";

    if (t.IsGenericType)
    {
        var name = t.Name;
        var tick = name.IndexOf('`');
        if (tick > 0) name = name[..tick];

        var args = t.GetGenericArguments().Select(FormatType);
        return $"{name}<{string.Join(", ", args)}>";
    }

    return t.Name;
}

bool TryGetCollectionElement(Type t, out Type elem)
{
    elem = null!;

    if (t == typeof(string)) return false;

    if (t.IsArray)
    {
        elem = t.GetElementType()!;
        return true;
    }

    // IEnumerable<T>
    var ie = t.GetInterfaces()
        .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));

    if (ie != null)
    {
        elem = ie.GetGenericArguments()[0];
        return true;
    }

    return false;
}

string Kind(Type t)
{
    if (t.IsInterface) return "interface";
    if (t.IsEnum) return "enum";
    if (t.IsAbstract) return "abstract class";
    return "class";
}

static IEnumerable<PropertyInfo> PublicProps(Type t)
    => t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
        .Where(p => p.GetMethod != null && !p.GetMethod.IsStatic);

static IEnumerable<FieldInfo> PublicFields(Type t)
    => t.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
        .Where(f => !f.IsStatic);

var lines = new List<string>();
lines.Add("@startuml");
lines.Add("hide circle");
lines.Add("skinparam classAttributeIconSize 0");
lines.Add("");

var declared = new HashSet<string>();

foreach (var t in scopeTypes)
{
    var name = SimpleName(t);
    if (!declared.Add(name)) continue;

    lines.Add($"{Kind(t)} {name} {{");

    // properties
    foreach (var p in PublicProps(t))
        lines.Add($"  + {p.Name} : {FormatType(p.PropertyType)}");

    // fields
    foreach (var f in PublicFields(t))
        lines.Add($"  + {f.Name} : {FormatType(f.FieldType)}");

    lines.Add("}");
    lines.Add("");
}

// relations
var rel = new HashSet<string>();

foreach (var t in scopeTypes)
{
    var from = SimpleName(t);

    // inheritance
    var baseT = t.BaseType;
    if (baseT != null && baseT != typeof(object) && InScope(baseT))
    {
        var to = SimpleName(baseT);
        var r = $"{to} <|-- {from}";
        rel.Add(r);
    }

    // interfaces
    foreach (var it in t.GetInterfaces().Where(InScope))
    {
        var to = SimpleName(it);
        var r = $"{to} <|.. {from}";
        rel.Add(r);
    }

    // associations via props/fields
    foreach (var p in PublicProps(t))
    {
        var pt = p.PropertyType;
        string mult = "";
        Type target = pt;

        if (TryGetCollectionElement(pt, out var elem))
        {
            target = elem;
            mult = "\"*\"";
        }

        if (InScope(target) && target != t)
        {
            var to = SimpleName(target);
            var r = mult == "" 
                ? $"{from} --> {to} : {p.Name}"
                : $"{from} --> {mult} {to} : {p.Name}";
            rel.Add(r);
        }
    }

    foreach (var f in PublicFields(t))
    {
        var ft = f.FieldType;
        string mult = "";
        Type target = ft;

        if (TryGetCollectionElement(ft, out var elem))
        {
            target = elem;
            mult = "\"*\"";
        }

        if (InScope(target) && target != t)
        {
            var to = SimpleName(target);
            var r = mult == ""
                ? $"{from} --> {to} : {f.Name}"
                : $"{from} --> {mult} {to} : {f.Name}";
            rel.Add(r);
        }
    }
}

lines.AddRange(rel.OrderBy(x => x));
lines.Add("");
lines.Add("@enduml");

Directory.CreateDirectory(Path.GetDirectoryName(outFile)!);
File.WriteAllLines(outFile, lines);

Console.WriteLine("Wrote: " + outFile);