using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Xamarin.Forms;

namespace DefinitionGenerator
{
    public class Generator
    {
        private string name;
        private readonly string @namespace;
        private string filePath;

        private IndentedTextWriter writer;

        private List<Type> imports = new List<Type>();
        // private List<Type> exports = new List<Type>();
        private List<NamespaceItem> others = new List<NamespaceItem>();
        private List<string> defaultNamespaces = new List<string>();
        private Assembly Assembly;
        private Dictionary<Type, string> names = new Dictionary<Type, string>();

        static Type[] rootTypes = new Type[] {
                typeof(Xamarin.Forms.BindableObject),
                typeof(Xamarin.Forms.Element),
                typeof(Xamarin.Forms.ElementTemplate),
                typeof(Xamarin.Forms.Color),
                typeof(Xamarin.Forms.Style),
                typeof(Xamarin.Forms.StyleSheets.StyleSheet),
                typeof(Xamarin.Forms.Easing),
                typeof(Xamarin.Forms.Effect),
                typeof(Xamarin.Forms.IValueConverter)
            };

        public Generator(string name, string @namespace, string filePath)
        {
            this.name = name;
            this.@namespace = @namespace;
            this.filePath = filePath;
        }

        internal string Generate()
        {
            var sw = new StringWriter();
            writer = new IndentedTextWriter(sw);

            ResolveTypes();

            WriteImports(writer);

            foreach(var ns in others)
            {
                WriteNamespaceDefinition(ns);
            }

            foreach (var ns in others)
            {
                WriteTypes(ns, writer);
            }

            WriteExport(writer);

            writer.WriteLine();

            return sw.ToString();
        }

        private void WriteExport(IndentedTextWriter writer)
        {
            
        }

        private void WriteNamespaceDefinition(NamespaceItem nsItem)
        {
            bool isDefault = nsItem.FullName == nsItem.Name && nsItem.Name == this.name;
            if (isDefault)
            {
                writer.WriteLine($"namespace {nsItem.Name} {{");
            } else
            {
                writer.WriteLine($"export namespace {nsItem.Name} {{");
            }
            writer.Indent++;

            foreach (var t in nsItem.Types)
            {
                WriteType(writer, t, nsItem);
            }

            foreach(var c in nsItem.Children)
            {
                WriteNamespaceDefinition(c);
            }

            writer.Indent--;
            writer.WriteLine("}");

            if (isDefault)
            {
                writer.WriteLine();
                writer.WriteLine($"export default {this.name};");
            }
            

        }

        private void WriteTypes(NamespaceItem ns, IndentedTextWriter writer)
        {
            if (ns.Types.Count > 0)
            {
                writer.WriteLine($"ns = \"{ns.FullName}\";");
                writer.WriteLine($"Object.defineProperties({ns.FullName} as any, {{");
                writer.Indent++;
                foreach (var t in ns.Types)
                {
                    var name = t.ToClassName();
                    var nsName = t.Namespace == ns.FullName
                        ? "ns"
                        : $"\"{t.Namespace}\"";
                    writer.WriteLine($"{name}: create(\"{t.Name}\",{nsName}),");
                    //writer.WriteLine($"{name}: {{ ");
                    //writer.Indent++;
                    //writer.WriteLine("configurable: true,");
                    //writer.WriteLine("enumerable: true,");
                    //writer.WriteLine($"get() {{");
                    //writer.Indent++;
                    //writer.WriteLine($"const t = bridge.getClass(\"{t.AssemblyQualifiedName}\");");
                    //writer.WriteLine($"Object.defineProperty(this, \"{name}\", {{ value: t, enumerable: true, writable: true, configurable: true }})");
                    //writer.WriteLine("return t;");
                    //writer.Indent--;
                    //writer.WriteLine("}");
                    //writer.Indent--;
                    //writer.WriteLine("},");
                }
                writer.Indent--;
                writer.WriteLine("});");
            }

            foreach(var child in ns.Children)
            {
                WriteTypes(child, writer);
            }
        }

        private void WriteType(IndentedTextWriter writer, Type t, NamespaceItem nsItem)
        {

            var bt = t.BaseType == typeof(object) ? null : t.BaseType;
            if (bt == null)
            {
                writer.WriteLine($"export declare class {t.ToClassName()} extends RootObject {{");
            } else {
                writer.WriteLine($"export declare class {t.ToClassName()} extends {GetTypeName(bt, nsItem)} {{");
            }
            writer.Indent++;
            // expose all static properties...
            foreach(var p in t.GetProperties(BindingFlags.Public | BindingFlags.DeclaredOnly | BindingFlags.Static))
            {
                writer.WriteLine($"public static {p.Name.ToCamelCase()}: {GetTypeName(p.PropertyType, nsItem)};");
            }

            Dictionary<string, string> attached = new Dictionary<string, string>();

            // expose all static methods...
            foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.DeclaredOnly | BindingFlags.Static))
            {
                if (m.IsAttachedMethod())
                {
                    var name = m.ToAttachedName();
                    attached[name] = name;
                    writer.WriteLine($"public static {name}: AttachedNode;");
                }
            }

            // expose all BindableProperty
            foreach(var p in t.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                if (p.FieldType == typeof(BindableProperty))
                {
                    var name = p.Name.ToCamelCase();
                    if (name.EndsWith("Property"))
                    {
                        name = name.Substring(0, name.Length - "Property".Length);
                    }
                    if (attached.ContainsKey(name))
                        continue;
                    attached[name] = name;
                    writer.WriteLine($"public static {name}: AttachedNode;");
                } else
                {
                    writer.WriteLine($"public static {p.Name.ToCamelCase()}: {GetTypeName(p.FieldType, nsItem)};");
                }
            }

            foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.DeclaredOnly | BindingFlags.Instance))
            {
                var name = p.Name.ToCamelCase();
                if (p.PropertyType.IsCollection())
                {
                    if (attached.ContainsKey(name))
                        continue;
                    attached[name] = name;
                    writer.WriteLine($"public static {name}: AttachedNode;");
                    continue;
                }

                writer.WriteLine($"public {name}: {GetTypeName(p.PropertyType, nsItem)};");

                if (rootTypes.Any(t => t.IsAssignableFrom(p.PropertyType)))
                {
                    if (attached.ContainsKey(name))
                        continue;
                    attached[name] = name;
                    writer.WriteLine($"public static {name}: AttachedNode;");
                }
            }


            writer.Indent--;
            writer.WriteLine("}");
        }

        private string GetTypeName(Type type, NamespaceItem nsItem)
        {

            if (type.IsEnum)
            {
                return string.Join(" | ", type.GetEnumNames().Select(x => JsonSerializer.Serialize(x))) + " | string | number | null | undefined | Bind";
            }

            var t = Type.GetTypeCode(type);
            switch (t)
            {
                case TypeCode.Boolean:
                    return "boolean | null | Bind";
                case TypeCode.Char:
                case TypeCode.String:
                    return "string | null | Bind";
                case TypeCode.SByte:
                case TypeCode.Byte:
                case TypeCode.UInt16:
                case TypeCode.Int32:
                case TypeCode.Int64:
                case TypeCode.UInt64:
                case TypeCode.Single:
                case TypeCode.Double:
                case TypeCode.Decimal:
                case TypeCode.UInt32:
                case TypeCode.Int16:
                    return "number | null | Bind";
                case TypeCode.DateTime:
                    return "Date | null | Bind";
            }

            if (type.Assembly == this.Assembly)
            {
                if(defaultNamespaces.Contains(type.Namespace))
                {
                    if (names.ContainsKey(type))
                    {
                        return this.name + "." + type.ToClassName();
                    }
                    if (type.IsConstructedGenericType)
                    {
                        if (names.ContainsKey(type.GetGenericTypeDefinition()))
                        {
                            return this.name + "." + type.GetGenericTypeDefinition().ToClassName();
                        }
                    }
                }

                return "RootObject";
                //while (type != null)
                //{
                //    if (names.ContainsKey(type))
                //    {
                //        if (type.IsConstructedGenericType)
                //        {
                //            // return type.Name + "$" + string.Join("_", type.GenericTypeArguments.Select(GetTypeName) );
                //            return "RootObject";
                //        }
                //        if (this.name == nsItem.Name && defaultNamespaces.Contains(type.Namespace))
                //        {
                //            return type.Name;
                //        }
                //        break;
                //    }
                //    if (type.BaseType == null || type.BaseType == typeof(object))
                //        break;
                //    type = type.BaseType;
                //}
            }
            // return type.Assembly.GetName().Name + "." + type.Name;
            if (type.Assembly != Assembly)
            {
                if (type.Namespace == "Xamarin.Forms")
                {
                    return "XF.default." + type.ToClassName();
                }
                if (type.Namespace == "Xamarin.Forms.Shapes")
                {
                    return "XF.default." + type.ToClassName();
                }
            }
            return $"RootObject /*{type.FullName}*/";
        }

        private void WriteImports(IndentedTextWriter writer)
        {
            writer.WriteLine(@"//tslint:disable
import XNode, { RootObject, NodeFactory, AttachedNode } from ""@web-atoms/core/dist/core/XNode"";
import Bind from ""@web-atoms/core/dist/core/Bind"";
import { ColorItem } from ""@web-atoms/core/dist/core/Colors"";
");
            if(typeof(Xamarin.Forms.View).Assembly != Assembly)
            {
                writer.WriteLine("import * as XF from \"./XF\";");
            }

            writer.WriteLine("declare var bridge: any;");
            writer.WriteLine($"const assemblyName = `{this.Assembly.FullName}`;");
            writer.WriteLine($"let ns = ``;");
            writer.WriteLine(@"function create(name: string, ns: string) {
    return {
        configurable: true,
        enumerable: true,
        get() {
            const t = bridge.getClass(`${ns}.${name}, ${assemblyName}`); 
            Object.defineProperty(this, name, {
                configurable: true,
                enumerable: true,
                writable: true,
                value: t
            });
            return t;
        }
    };
}
");

        }

        private void ResolveTypes()
        {
            // var resolver = new PathAssemblyResolver(new string[] { });
            // var a = new MetadataLoadContext(resolver).LoadFromAssemblyPath(this.filePath);
            Assembly a;
            if (System.IO.File.Exists(this.filePath))
            {
                a = Assembly.LoadFrom(this.filePath);
            } else
            {
                a = Assembly.Load(this.filePath);
            }

            this.Assembly = a;



            foreach(var d in a.GetCustomAttributes<XmlnsDefinitionAttribute>())
            {
                this.defaultNamespaces.Add(d.ClrNamespace);
            }

            if(a == typeof(Xamarin.Forms.BindableObject).Assembly)
            {
                this.defaultNamespaces.Add("Xamarin.Forms.StyleSheets");
            }

            if(this.defaultNamespaces.Count == 0)
            {
                this.defaultNamespaces.Add(this.name);
            }

           

            foreach(var t in a.ExportedTypes)
            {
                if (rootTypes.Any(x => x.IsAssignableFrom(t))
                    ||
                    t.HasAttachedMethod()
                    )
                {
                    if (this.defaultNamespaces.Contains(t.Namespace))
                    {
                        AddOtherType(others, t, this.name);
                    } else
                    {
                        AddOtherType(others, t, t.Namespace);
                    }
                    if(t.BaseType != null && t.BaseType.Assembly != a)
                    {
                        this.imports.Add(t.BaseType);
                    }
                }
            }
        }

        private void AddOtherType(List<NamespaceItem> children, Type t, string nsName)
        {
            var nss = nsName.Split('.');
            var start = children;
            NamespaceItem ns = null;
            foreach(var name in nss)
            {
                var child = start.FirstOrDefault(x => x.Name == name);
                if(child == null)
                {
                    child = new NamespaceItem { 
                        Name = name,
                        FullName = ns == null ? name : ($"{ns.FullName}.{name}")
                    };
                    start.Add(child);
                }
                start = child.Children;
                ns  = child;
            }
            ns.Types.Add(t);
            names[t] = this.name == ns.Name ? t.Name : ns.FullName + "." + t.Name; 
        }
    }

    class NamespaceItem
    {
        public string Name { get; set; }

        public string FullName { get; set; }

        public List<Type> Types { get; set; } = new List<Type>();

        public List<NamespaceItem> Children { get; set; } = new List<NamespaceItem>();
    }
}
