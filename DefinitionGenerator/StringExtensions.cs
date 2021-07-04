using System;
using System.Collections.Generic;
using System.Text;

namespace DefinitionGenerator
{
    public static class StringExtensions
    {

        public static string ToCamelCase(this string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return value;
            return Char.ToLower(value[0]) + value.Substring(1);
        }


    }
}
