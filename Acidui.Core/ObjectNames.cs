using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;

namespace Acidui
{
    [DebuggerDisplay("{Simple}")]
    public class ObjectName : IEquatable<ObjectName>
    {
        String[] parts;

        String simpleName;

        String escapedName;

        public String Escaped => escapedName ?? throw new Exception("root object name can't be used in SQL");

        public String Simple => simpleName;

        public String LastPart => parts[parts.Length - 1];

        public IEnumerable<String> ReversedParts => parts.Reverse();

        public static ObjectName RootName = new ObjectName();

        private ObjectName()
        {
            parts = new[] { "" };

            simpleName = "";
        }

        public ObjectName(params String[] parts)
        {
            if (parts.Length == 0) throw new Exception("Object names must have at least one part");

            foreach (var part in parts)
            {
                if (part.Length == 0) throw new Exception("Object name parts can't be empty");

                part.Assert(NameHarmlessness);
            }

            this.parts = parts;

            escapedName = String.Join(".", from p in parts select p.EscapeNamePart());

            simpleName = String.Join(".", from p in parts select p.Replace(".", "_.._"));
        }

        static readonly Char[] dangerousCharacters = "[]\"".ToArray();

        static Boolean NameHarmlessness(String name)
        {
            return name.IndexOfAny(dangerousCharacters) < 0;
        }

        public bool Equals([AllowNull] ObjectName other) => other?.escapedName == escapedName;

        public override bool Equals(object obj) => Equals(obj as ObjectName);

        public override int GetHashCode() => escapedName?.GetHashCode() ?? 42;

        public static bool operator ==(ObjectName lhs, ObjectName rhs) => lhs.Equals(rhs);
        public static bool operator !=(ObjectName lhs, ObjectName rhs) => !lhs.Equals(rhs);

        public override string ToString() => throw new NotImplementedException("Use an explicit way to stringify an ObjectName");
    }

    public class ObjectNameParser
    {
        public ObjectName Parse(String name)
        {
            var parts = name.Split("].[");

            ref var first = ref parts[0];
            ref var last = ref parts[parts.Length - 1];

            first = first.TrimStart('[');
            last = last.TrimEnd(']');

            return new ObjectName(parts);
        }
    }

    public static class ObjectNames
    {
        public static String EscapeNamePart(this String p)
        {
            return $"[{p.Replace("]", "]]")}]";
        }

        public static ObjectName GetName(this IISObjectNamable namable)
        {
            return new ObjectName(new[] { namable.Catalog, namable.Schema, namable.Name }.Where(n => n != null).ToArray());
        }

        public static void SetName(this IISObjectNamable namable, ObjectName name)
        {
            var parts = name.ReversedParts;

            namable.Name = parts.First();
            namable.Schema = parts.Skip(1).FirstOrDefault();
            namable.Catalog = parts.Skip(2).FirstOrDefault();
        }
    }
}
