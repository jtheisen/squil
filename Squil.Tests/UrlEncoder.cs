using static Squil.UrlEncoder;

namespace Squil.Tests;

[TestClass]
public class UrlEncoder
{
    [TestMethod]
    public void TestSpecialCharacters()
    {
        Assert.AreEqual("foo/bar%20baz/", UrlEncodePath("foo/bar baz/"));
        Assert.AreEqual("foo%2fbar%20baz", UrlEncodePathPart("foo/bar baz"));

        Assert.AreEqual("foo/bar", UrlEncodeQueryKey("foo/bar"));
        Assert.AreEqual("foo%26bar", UrlEncodeQueryKey("foo&bar"));
        Assert.AreEqual("foo?bar", UrlEncodeQueryKey("foo?bar"));

        Assert.AreEqual("foo%26bar", UrlEncodeQueryValue("foo&bar"));
        Assert.AreEqual("foo=bar", UrlEncodeQueryValue("foo=bar"));

        Assert.AreEqual("foo%20bar", UrlEncodeFragment("foo bar"));
        Assert.AreEqual("foo/bar", UrlEncodeFragment("foo/bar"));
        Assert.AreEqual("foo&bar", UrlEncodeFragment("foo&bar"));
        Assert.AreEqual("foo#bar", UrlEncodeFragment("foo#bar"));
    }

    [TestMethod]
    public void TestEncoding()
    {
        Assert.AreEqual("H%c3%a4user", UrlEncodePathPart("Häuser"));
        Assert.AreEqual("%f0%92%8a%95", UrlEncodePathPart("𒊕"));
    }

}
