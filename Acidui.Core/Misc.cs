using System;
using System.Collections.Generic;
using System.Text;

namespace Acidui
{
    //public class ObjectName : IEquatable<String>
    //{
    //    private readonly string name;

    //    public static String RootName = new String();

    //    private ObjectName()
    //    {
    //    }

    //    public ObjectName(String name)
    //    {
    //        name.Assert(n => !String.IsNullOrWhiteSpace(name), "Name must not be null or whitespace");

    //        this.name = name;
    //    }

    //    public String SqlName => name ?? throw new InvalidOperationException("The root name can't be appear in SQL");

    //    public String XmlName => name.Replace('.', '_');

    //    public bool Equals(String other) => other?.name == name;

    //    public override string ToString() => name ?? "<root>";

    //    public override bool Equals(object obj) => base.Equals(obj as String);

    //    public override int GetHashCode() => name?.GetHashCode() ?? 0;

    //    public static Boolean operator ==(String lhs, String rhs) => lhs.Equals(rhs);
    //    public static Boolean operator !=(String lhs, String rhs) => !lhs.Equals(rhs);

    //    public static implicit operator String(String name)
    //        => new String(name);
    //}
}
