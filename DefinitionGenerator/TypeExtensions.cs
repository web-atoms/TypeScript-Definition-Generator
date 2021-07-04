using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Xamarin.Forms;

namespace DefinitionGenerator
{
    public static class TypeExtensions
    {

        public static string ToClassName(this Type type)
        {
            if (type.IsConstructedGenericType)
                return type.Name.Split('`')[0]+"$Generic";
            if (type.IsGenericTypeDefinition)
                return type.Name.Split('`')[0]+"$Generic";
            return type.Name;
        }

        public static bool HasAttachedMethod(this Type type)
        {
            var ms = type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.DeclaredOnly);
            return ms.Any(IsAttachedMethod);
        }

        public static bool IsAttachedMethod(this MethodInfo method)
        {
            var ps = method.GetParameters();
            if (ps.Length == 2)
            {
                var first = ps[0];
                if (typeof(BindableObject).IsAssignableFrom(first.ParameterType))
                {
                    return true;
                }
            }
            return false;
        }

        public static string ToAttachedName(this MethodInfo method)
        {
            return method.Name.Substring(3).ToCamelCase();
        }

        public static bool IsCollection(this Type type)
        {
            if (typeof(System.Collections.ICollection).IsAssignableFrom(type))
                return true;
            if (typeof(System.Collections.IList).IsAssignableFrom(type))
                return true;
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ICollection<>))
                return true;
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IList<>))
                return true;
            if (type.IsInterface)
                return false;
            return type.GetInterfaces().Any(IsCollection);
        }

    }
}
