using System.Reflection;
using System.Xml.Serialization;

namespace Squil.SchemaBuilding;

[AttributeUsage(AttributeTargets.Class)]
public class XmlTableAttribute : Attribute
{
    public String Table { get; }

    public XmlTableAttribute(String table)
    {
        Table = table;
    }
}

[AttributeUsage(AttributeTargets.Property)]
public class XmlNoColumnAttribute : Attribute
{
}

public class LazyStaticSingleton<T>
    where T : new()
{
    static Lazy<T> creator = new Lazy<T>(() => new T());

    public static T Instance => creator.Value;
}

public interface ILazyStaticSingleton<T>
    where T : new()
{
    static Lazy<T> creator = new Lazy<T>(() => new T());

    public static T Instance => creator.Value;
}

public class XmlEntitiyMetata<E> : LazyStaticSingleton<XmlEntitiyMetata<E>>
    where E : class
{
    XmlTypeAttribute typeAttribute;
    XmlTableAttribute tableAttribute;

    public XmlProperty[] XmlProperties { get; }

    public CsdColumn[] Columns { get; }

    public String TableName => tableAttribute.Table;

    public struct XmlProperty
    {
        public PropertyInfo p;
        public XmlAttributeAttribute a;

        public String Name => String.IsNullOrWhiteSpace(a?.AttributeName) ? p.Name : a.AttributeName;
    }

    public XmlEntitiyMetata()
    {
        var type = typeof(E);

        typeAttribute = type.GetCustomAttribute<XmlTypeAttribute>();
        tableAttribute = type.GetCustomAttribute<XmlTableAttribute>();

        var hierarchy = GetAncestors(type).Reverse().ToArray();

        if (String.IsNullOrWhiteSpace(typeAttribute?.TypeName))
        {
            throw new Exception($"Missing type attribute or name on attribute for type {type.Name}");
        }

        if (String.IsNullOrWhiteSpace(tableAttribute?.Table))
        {
            throw new Exception($"Missing table attribute or name on attribute for type {type.Name}");
        }

        XmlProperties = (
            from c in hierarchy
            from p in c.GetProperties(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            from a in p.GetCustomAttributes<XmlAttributeAttribute>()
            where p.GetCustomAttribute<XmlNoColumnAttribute>() == null
            select new XmlProperty { p = p, a = a }
        ).ToArray();

        Columns = XmlProperties.Select(p => new CsdColumn
        {
            Name = p.Name,
            IsIgnoredByDefault = !p.p.GetMethod.IsPublic,
            DataType = "varchar"
        }
        ).ToArray();
    }

    public static IEnumerable<Type> GetAncestors(Type type)
    {
        while (type != null)
        {
            yield return type;
            type = type.BaseType;
        }
    }
}
