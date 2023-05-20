using Avalonia.Controls.Shapes;
using Avalonia.Data;
using ER_Diagram;
using CircuitSimulator.Models;
using ReactiveUI;
using System.Reflection;
using System.Runtime.Serialization.Formatters;


// CircuitSimulator.Program.Main(Array.Empty<string>());
// Type[] types = Assembly.GetExecutingAssembly().GetTypes();
// Type[] types = new Type[] { typeof(Mapper) };

Type[] types = (Assembly.GetAssembly(typeof(Mapper)) ?? throw new Exception("Чё?!")).GetTypes();
List<Type> passed_types = new();
Dictionary<Type, int> type_to_num = new();

List<object> items = new();
int n = 0;
foreach (Type type in types) {
    var name = type.FullName ?? throw new Exception("Чё?!");
    if (name.Contains('+') || !name.StartsWith("CircuitSimulator.")) continue;

    Console.WriteLine("\nT: " + name);
    passed_types.Add(type);
    type_to_num[type] = n;

    List<object> attrs = new(), meths = new();
    Dictionary<string, object?> data = new() {
        ["name"] = type.Name,
        ["stereo"] =
            type.IsAbstract ?
                (type.IsSealed ? 1 /* static */ : 2 /* abstract */ ) :
            type.IsInterface ? 3 /* interface */ : 0,
        ["access"] = type.IsPublic ? 1 :
                     type.IsAbstract ? 2 :
                     type.IsInterface ? 3 :
                     0, // private
        ["attributes"] = attrs,
        ["methods"] = meths,
    };
    List<object> item = new() { data, 25 + 225 * (n % 7), 25 + 175 * (n / 7), 200, 150 };
    n++;
    items.Add(item);

    foreach (var mem in type.GetMembers()) {
        if (mem.Module.Name != "CircuitSimulator.dll") continue;
        string mem_name = mem.Name;

        Console.WriteLine($"  {mem_name,-36} | {mem.MemberType,-12} | {mem.DeclaringType == type}");
        if (mem.DeclaringType != type) continue;

        var meth_arr = type.GetMethods();
        var ctor_arr = type.GetConstructors();

        switch (mem.MemberType) {
        case MemberTypes.Field:
            var field_info = type.GetField(mem_name) ?? throw new Exception("Чё?!");

            attrs.Add(new Dictionary<string, object?>() {
                ["name"] = mem.Name,
                ["type"] = Functions.TypeRenamer(field_info.FieldType),
                ["access"] = field_info.IsPrivate ? 0 : // private
                             field_info.IsPublic ? 1 : // public
                             field_info.IsFamily ? 2 : // protected
                             field_info.IsAssembly ? 3 /* package */ : 0,
                ["readonly"] = field_info.IsInitOnly,
                ["static"] = field_info.IsStatic,
                ["stereo"] = 0, // common
                ["default"] = "", //    :/
            });
            break;
        case MemberTypes.Event:
            var event_info = type.GetEvent(mem_name) ?? throw new Exception("Чё?!");

            attrs.Add(new Dictionary<string, object?>() {
                ["name"] = mem.Name,
                ["type"] = Functions.TypeRenamer(event_info.EventHandlerType),
                ["access"] = 1, // public
                ["readonly"] = false,
                ["static"] = false,
                ["stereo"] = 1, // event
                ["default"] = "", //    :/
            });
            break;
        case MemberTypes.Property:
            var prop_info = type.GetProperty(mem_name) ?? throw new Exception("Чё?!");
            var getter = prop_info.GetGetMethod(true);
            var setter = prop_info.GetSetMethod(true);

            attrs.Add(new Dictionary<string, object?>() {
                ["name"] = mem.Name,
                ["type"] = Functions.TypeRenamer(prop_info.PropertyType),
                ["access"] = getter != null ?
                    getter.IsPrivate ? 0 : // private
                    getter.IsPublic ? 1 : // public
                    getter.IsFamily ? 2 : // protected
                    getter.IsAssembly ? 3 /* package */ : 0 :
                             setter != null ?
                    setter.IsPrivate ? 0 : // private
                    setter.IsPublic ? 1 : // public
                    setter.IsFamily ? 2 : // protected
                    setter.IsAssembly ? 3 /* package */ : 0 : 0,
                ["readonly"] = false,
                ["static"] = prop_info.IsStatic(),
                ["stereo"] = 2, // property
                ["default"] = "{" +
                        (getter != null ?
                    (getter.IsPrivate ? "private" :
                    getter.IsPublic ? "public" :
                    getter.IsFamily ? "protected" :
                    getter.IsAssembly ? "package" : "?") + " get; " : "") +
                        (setter != null ?
                    (setter.IsPrivate ? "private" :
                    setter.IsPublic ? "public" :
                    setter.IsFamily ? "protected" :
                    setter.IsAssembly ? "package" : "?") + " set; " : "") +
                "}",
            });
            break;
        case MemberTypes.Method:
            foreach (var method_info in meth_arr.Where(x => x.Name == mem_name)) {
                List<object> props = new();
                foreach (var param in method_info.GetParameters()) {
                    props.Add(new Dictionary<string, object?>() {
                        ["name"] = param.Name,
                        ["type"] = Functions.TypeRenamer(param.ParameterType),
                        ["default"] = param.DefaultValue + "",
                    });
                }

                meths.Add(new Dictionary<string, object?>() {
                    ["name"] = mem.Name,
                    ["type"] = Functions.TypeRenamer(method_info.ReturnType),
                    ["access"] =
                        method_info.IsPrivate ? 0 : // private
                        method_info.IsPublic ? 1 : // public
                        method_info.IsFamily ? 2 : // protected
                        method_info.IsAssembly ? 3 /* package */ : 0,
                    ["stereo"] =
                        method_info.IsStatic ? 1 :
                        method_info.IsAbstract ? 2 :
                        0, // common/virtual
                    ["props"] = props,
                });
            }
            break;
        case MemberTypes.Constructor:
            foreach (var ctor_info in ctor_arr.Where(x => x.Name == mem_name)) {
                List<object> props = new();
                foreach (var param in ctor_info.GetParameters()) {
                    props.Add(new Dictionary<string, object?>() {
                        ["name"] = param.Name,
                        ["type"] = Functions.TypeRenamer(param.ParameterType),
                        ["default"] = param.DefaultValue + "",
                    });
                }

                meths.Add(new Dictionary<string, object?>() {
                    ["name"] = mem.Name,
                    ["type"] = "self",
                    ["access"] =
                        ctor_info.IsPrivate ? 0 : // private
                        ctor_info.IsPublic ? 1 : // public
                        ctor_info.IsFamily ? 2 : // protected
                        ctor_info.IsAssembly ? 3 /* package */ : 0,
                    ["stereo"] = 3, // create
                    ["props"] = props,
                });
            }
            break;
        default:
            Console.WriteLine("!!!Пропущено!!!\n");
            break;
        }
    }
}

List<object> joins = new();
n = 0;
foreach (Type type in passed_types) {
    Type Base = type.BaseType ?? typeof(object);
    int me_num = n++;
    if (type_to_num.TryGetValue(Base, out int num))
        joins.Add(new object[] { me_num, 3, 1.0, num, 0, 0.0, 0 });

    foreach (var i_type in type.GetInterfaces())
        if (type_to_num.TryGetValue(i_type, out int num2))
            joins.Add(new object[] { me_num, 3, 1.0, num2, 0, 0.0, 1 });

    List<Type> u_type_arr = new();
    Functions.GetUsedTypes(type, u_type_arr, 0);
    foreach (var u_type in u_type_arr)
        if (type_to_num.TryGetValue(u_type, out int num3) && me_num != num3)
            joins.Add(new object[] { me_num, 3, 1.0, num3, 0, 0.0, 2 });

    List<Type> cont_type_arr = new();
    Functions.GetUsedTypes(type, cont_type_arr, 1);
    foreach (var cont_type in cont_type_arr)
        if (type_to_num.TryGetValue(cont_type, out int num3) && me_num != num3)
            joins.Add(new object[] { num3, 3, 1.0, me_num, 0, 0.0, 4 });
}

List<object> f_joins = new();
Dictionary<long, bool> used_joins = new();
foreach (var obj in joins) {
    var join = (object[]) obj;
    long pack = ((int) join[0]) << 34 | ((int) join[3]) << 4 | ((int) join[6]);
    if (used_joins.ContainsKey(pack)) continue;

    used_joins[pack] = true;
    f_joins.Add(obj);
}

Dictionary<string, object?> res = new() {
    ["items"] = items,
    ["joins"] = f_joins,
};

File.WriteAllText("../../../../../DiagramEditor/DiagramEditor/Export.json", Utils.Obj2json(res));