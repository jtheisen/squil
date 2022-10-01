namespace Squil.Tests;

[TestClass]
public class SqlServerXmlEscaping
{
    [TestMethod]
    public void TestEscape()
    {
        Assert.AreEqual("foo", "foo".EscapeSqlServerXmlName());
        Assert.AreEqual("foo-bar", "foo-bar".EscapeSqlServerXmlName());
        Assert.AreEqual("foo.bar", "foo.bar".EscapeSqlServerXmlName());
        Assert.AreEqual("_x002E_foo", ".foo".EscapeSqlServerXmlName());
        Assert.AreEqual("_foo", "_foo".EscapeSqlServerXmlName());
        Assert.AreEqual("_x005F_xena", "_xena".EscapeSqlServerXmlName());
        Assert.AreEqual("_x005F_x005F_x005F_xena", "_x005F_xena".EscapeSqlServerXmlName());

        Assert.AreEqual("_x005B_foo_x005D_", "[foo]".EscapeSqlServerXmlName());
    }

    [TestMethod]
    public void TestUnescape()
    {
        Assert.AreEqual("foo", "foo".UnescapeSqlServerXmlName());
        Assert.AreEqual(".foo", "_x002E_foo".UnescapeSqlServerXmlName());
        Assert.AreEqual("_xena", "_x005F_xena".UnescapeSqlServerXmlName());
        Assert.AreEqual("_x005F_xena", "_x005F_x005F_x005F_xena".UnescapeSqlServerXmlName());
    }
}
