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
        private List<Type> exports = new List<Type>();
        private List<Type> others = new List<Type>();
        private Assembly Assembly;

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

            WriteDefinitions(writer);

            WriteTypes(writer);

            WriteExport(writer);

            writer.WriteLine($"export default {this.name};");
            writer.WriteLine();

            return sw.ToString();
        }

        private void WriteExport(IndentedTextWriter writer)
        {
            
        }

        private void WriteDefinitions(IndentedTextWriter writer)
        {
            writer.WriteLine($"namespace {this.name} {{");
            writer.Indent++;

            foreach (var t in exports)
            {
                WriteType(writer, t);
            }

            writer.Indent--;
            writer.WriteLine("}");
        }

        private void WriteTypes(IndentedTextWriter writer)
        {
            writer.WriteLine($"({this.name} as any) = {{");
            writer.Indent++;
            foreach (var t in exports)
            {
                var name = t.ToClassName();
                writer.WriteLine($"get {name}() {{");
                writer.Indent++;
                writer.WriteLine($"const t = bridge.getClass(\"{t.AssemblyQualifiedName}\");");
                writer.WriteLine($"Object.defineProperty(this, \"{name}\", {{ value: t, enumerable: true, writable: true, configurable: true }})");
                writer.WriteLine("return t;");
                writer.Indent--;
                writer.WriteLine("},");
            }
            writer.Indent--;
            writer.WriteLine("}");
        }

        private void WriteType(IndentedTextWriter writer, Type t)
        {

            var bt = t.BaseType;
            if (bt == typeof(object))
            {
                writer.WriteLine($"export declare class {t.ToClassName()} extends RootObject {{");
            } else {
                writer.WriteLine($"export declare class {t.ToClassName()} extends {GetTypeName(bt)} {{");
            }
            writer.Indent++;
            // expose all static properties...
            foreach(var p in t.GetProperties(BindingFlags.Public | BindingFlags.DeclaredOnly | BindingFlags.Static))
            {
                writer.WriteLine($"public static {p.Name.ToCamelCase()}: {GetTypeName(p.PropertyType)};");
            }
            // expose all static methods...
            foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.DeclaredOnly | BindingFlags.Static))
            {
                if (m.IsAttachedMethod())
                {
                    writer.WriteLine($"public static {m.ToAttachedName()}: AttachedNode;");
                }
            }

            // expose all BindableProperty
            foreach(var p in t.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                if (p.FieldType == typeof(BindableProperty))
                {
                    writer.WriteLine($"public static {p.Name.ToCamelCase()}: NodeFactory;");
                } else
                {
                    writer.WriteLine($"public static {p.Name.ToCamelCase()}: {GetTypeName(p.FieldType)};");
                }
            }

            foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.DeclaredOnly | BindingFlags.Instance))
            {
                if(p.PropertyType.IsCollection())
                {
                    writer.WriteLine($"public static {p.Name.ToCamelCase()}: AttachedNode;");
                    continue;
                }

                writer.WriteLine($"public {p.Name.ToCamelCase()}: {GetTypeName(p.PropertyType)};");
            }


            writer.Indent--;
            writer.WriteLine("}");
        }

        private string GetTypeName(Type type)
        {

            if (type.IsEnum)
            {
                return string.Join(" | ", type.GetEnumNames().Select( x => JsonSerializer.Serialize(x) )) + " | string | number | null | undefined | Bind";
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

            if(type.Assembly == this.Assembly)
            {
                while (type != null)
                {
                    if (exports.Any(x =>
                        x == type
                        || (type.IsConstructedGenericType && x == type.GetGenericTypeDefinition() )))
                    {
                        if(type.IsConstructedGenericType)
                        {
                            // return type.Name + "$" + string.Join("_", type.GenericTypeArguments.Select(GetTypeName) );
                            return "RootObject";
                        }
                        return type.Name;
                    }
                    type = type.BaseType;
                }
                return "RootObject";
            }
            // return type.Assembly.GetName().Name + "." + type.Name;
            return "any";
        }

        private void WriteImports(IndentedTextWriter writer)
        {
            writer.WriteLine(@"//tslint:disable
import XNode, { RootObject, NodeFactory, AttachedNode } from ""@web-atoms/core/dist/core/XNode"";
import Bind from ""@web-atoms/core/dist/core/Bind"";
import { ColorItem } from ""@web-atoms/core/dist/core/Colors"";
declare var bridge: any;
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

            var rootTypes = new Type[] {
                typeof(Xamarin.Forms.BindableObject),
                typeof(Xamarin.Forms.Element),
                typeof(Xamarin.Forms.ElementTemplate)
            };

            foreach(var t in a.ExportedTypes)
            {
                if (rootTypes.Any(x => x.IsAssignableFrom(t))
                    ||
                    t.HasAttachedMethod()
                    )
                {
                    if (t.Namespace == this.@namespace)
                    {
                        this.exports.Add(t);
                    } else
                    {
                        this.others.Add(t);
                    }
                    if(t.BaseType.Assembly != a)
                    {
                        this.imports.Add(t.BaseType);
                    }
                }
            }
        }
    }
}
