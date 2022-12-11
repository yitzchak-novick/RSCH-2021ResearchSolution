using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PyReporting
{
    public static class ExtensionMethods
    {
        public static String ToPyValList<T>(this IEnumerable<T> vals) => $"[{String.Join(", ", vals)}]";
        public static String ToPyStrList<T>(this IEnumerable<T> strs) => $"[{String.Join(", ", strs.Select(s => $"'{s}'"))}]";
    }
}
