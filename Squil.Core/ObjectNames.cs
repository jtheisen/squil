using Squil.SchemaBuilding;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;

namespace Squil
{
    [DebuggerDisplay("{Simple}")]
    public class ObjectName : IEquatable<ObjectName>
    {
        String[] parts;

        String simpleName;

        String escapedName;

        public String Escaped => escapedName ?? throw new Exception("root object name can't be used in SQL");

        public Boolean IsRootName => escapedName == null;

        public String Simple => simpleName;

        public String LastPart => parts[parts.Length - 1];

        public IEnumerable<String> ReversedParts => parts.Reverse();

        public static ObjectName RootName = new ObjectName();

        public void Deconstruct(out string catalog, out string schema, out string name)
        {
            if (parts.Length == 2)
            {
                catalog = null;

                Deconstruct(out schema, out name);
            }
            else
            {
                if (parts.Length != 3) throw new Exception($"3-deconstruction on {parts.Length}-part name");

                catalog = parts[0];
                schema = parts[1];
                name = parts[2];
            }
        }

        public void Deconstruct(out string schema, out string name)
        {
            if (parts.Length != 2) throw new Exception($"2-deconstruction on {parts.Length}-part name");

            schema = parts[0];
            name = parts[1];
        }

        private ObjectName()
        {
            parts = new[] { "" };

            simpleName = "";
        }

        public ObjectName(params String[] parts)
        {
            if (parts.Any(p => p == null))
            {
                parts = parts.Where(p => p != null).ToArray();
            }

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

        public String[] GetDistinguishedParts(ObjectName baseName)
        {
            if (baseName.IsRootName) return parts;

            if (IsRootName) throw new Exception("The root name has no parts");

            if (baseName.parts.Length != parts.Length) throw new Exception("Can't form a relative name from names of different length");

            var distinguishedParts = parts
                .Zip(baseName.parts)
                .SkipWhile(pair => pair.First == pair.Second)
                .Select(pair => pair.First)
                .ToArray()
                ;

            return distinguishedParts.Length == 0 ? new[] { LastPart } : distinguishedParts;
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

        public override string ToString() => $"--{simpleName}--";
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
    }
}
