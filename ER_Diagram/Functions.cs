using System.Reflection;

namespace ER_Diagram {
    internal class Functions {
        public static string TypeRenamer(Type? type) {
            if (type == null) return "???";

            var name = type.Name;
            var arr = name.Split('[');
            arr[0] = arr[0] switch {
                "Boolean" => "bool",
                "Byte" => "byte",
                "SByte" => "signed byte",
                "Char" => "char",
                "Int16" => "short",
                "Int32" => "int",
                "Int64" => "long",
                "Int128" => "long long",
                "Half" => "half float",
                "Single" => "float",
                "Double" => "double",
                "String" => "string",
                "Void" => "void",
                "Object" => "object",
                _ => arr[0]
            };

            var gen = type.GetGenericArguments();
            if (gen.Length > 0) arr[0] = arr[0].Split('\x60')[0] +
                "<" + string.Join(", ", gen.Select(TypeRenamer)) + ">";

            string res = string.Join('[', arr);
            if (res.EndsWith('&')) res = "ref " + res[..^1];
            return res;
        }

        public static void GetUsedTypes2(Type? type, List<Type> res, int mode) {
            if (type == null) return;
            if (mode == 0) res.Add(type);
            else { // mode == 1 or 2
                var gen = type.GetGenericArguments();
                foreach (var type2 in gen) GetUsedTypes2(type2, res, 2);
                if (gen.Length == 0 && mode == 2) res.Add(type);
            }
        }
        public static void GetUsedTypes(Type type, List<Type> res, int mode) {
            foreach (var mem in type.GetMembers()) {
                string mem_name = mem.Name;
                var meth_arr = type.GetMethods();
                var ctor_arr = type.GetConstructors();
                if (mem.DeclaringType != type) continue;

                switch (mem.MemberType) {
                case MemberTypes.Field:
                    var field_info = type.GetField(mem_name) ?? throw new Exception("Чё?!");
                    GetUsedTypes2(field_info.FieldType, res, mode);
                    break;
                case MemberTypes.Event:
                    var event_info = type.GetEvent(mem_name) ?? throw new Exception("Чё?!");
                    GetUsedTypes2(event_info.EventHandlerType, res, mode);
                    break;
                case MemberTypes.Property:
                    var prop_info = type.GetProperty(mem_name) ?? throw new Exception("Чё?!");
                    GetUsedTypes2(prop_info.PropertyType, res, mode);
                    break;
                case MemberTypes.Method:
                    foreach (var method_info in meth_arr.Where(x => x.Name == mem_name)) {
                        List<object> props = new();
                        foreach (var param in method_info.GetParameters())
                            GetUsedTypes2(param.ParameterType, res, mode);
                        GetUsedTypes2(method_info.ReturnType, res, mode);
                    }
                    break;
                case MemberTypes.Constructor:
                    foreach (var ctor_info in ctor_arr.Where(x => x.Name == mem_name)) {
                        List<object> props = new();
                        foreach (var param in ctor_info.GetParameters())
                            GetUsedTypes2(param.ParameterType, res, mode);
                    }
                    break;
                }
            }
        }
    }
}
