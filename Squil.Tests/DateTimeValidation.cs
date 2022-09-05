namespace Squil.Tests;

public abstract class DateOrTimeValidationTestBase
{
    protected DateOrTimeColumnType ct;

    protected abstract DateOrTimeColumnType CreateColumnType();

    public DateOrTimeValidationTestBase()
    {
        ct = CreateColumnType();
        ct.Init();
    }
}

[TestClass]
public class TimeValidation : DateOrTimeValidationTestBase
{
    protected override DateOrTimeColumnType CreateColumnType()
        => new DateOrTimeColumnType { Name = "time", WithTime = true };

    [TestMethod]
    public void TestBasicTimes()
    {
        ct.Validate("")
            .AssertOk("00:00:00", "23:59:59");

        ct.Validate("12")
            .AssertOk("12:00:00", "12:59:59");

        ct.Validate("12:30")
            .AssertOk("12:30:00", "12:30:59");

        ct.Validate("12:30:44")
            .AssertOk("12:30:44", "12:30:44");
    }

    [TestMethod]
    public void TestInvalid()
    {
        ct.Validate("3").AssertFail();
        ct.Validate("24").AssertFail();
        ct.Validate("00:6").AssertFail();
        ct.Validate("00:60").AssertFail();
        ct.Validate("00:00:6").AssertFail();
        ct.Validate("00:00:60").AssertFail();
        ct.Validate("00:00:00 ").AssertFail();
    }
}

[TestClass]
public class DateTimeValidation : DateOrTimeValidationTestBase
{
    protected override DateOrTimeColumnType CreateColumnType()
        => new DateOrTimeColumnType { Name = "datetime", WithDate = true, WithTime = true };

    [TestMethod]
    public void TestBasicDates()
    {
        ct.Validate("")
            .AssertOk("0001-01-01 00:00:00", "9999-12-31 23:59:59");

        ct.Validate("2001")
            .AssertOk("2001-01-01 00:00:00", "2001-12-31 23:59:59");

        ct.Validate("2000-02")
            .AssertOk("2000-02-01 00:00:00", "2000-02-29 23:59:59");

        ct.Validate("2002-02-15")
            .AssertOk("2002-02-15 00:00:00", "2002-02-15 23:59:59");
    }

    [TestMethod]
    public void TestBasicDateTimes()
    {
        ct.Validate("2001-02-03 01")
            .AssertOk("2001-02-03 01:00:00", "2001-02-03 01:59:59");

        ct.Validate("2001-02-03 01:02")
            .AssertOk("2001-02-03 01:02:00", "2001-02-03 01:02:59");

        ct.Validate("2001-02-03 01:02:03")
            .AssertOk("2001-02-03 01:02:03", "2001-02-03 01:02:03");
    }

    [TestMethod]
    public void TestDateTimeTrailer()
    {
        ct.Validate("2000-02-03 ")
            .AssertOk("2000-02-03 00:00:00", "2000-02-03 23:59:59");
    }
}

[TestClass]
public class DateValidation : DateOrTimeValidationTestBase
{
    protected override DateOrTimeColumnType CreateColumnType()
        => new DateOrTimeColumnType { Name = "date", WithDate = true };

    [TestMethod]
    public void TestBasicsDates()
    {
        ct.Validate("")
            .AssertOk("0001-01-01", "9999-12-31");

        ct.Validate("2001")
            .AssertOk("2001-01-01", "2001-12-31");

        ct.Validate("2000-02")
            .AssertOk("2000-02-01", "2000-02-29");

        ct.Validate("2002-02-15")
            .AssertOk("2002-02-15", "2002-02-15");
    }

    [TestMethod]
    public void TestDateTrailers()
    {
        ct.Validate("2000-02-")
            .AssertOk("2000-02-01", "2000-02-29");

        ct.Validate("2001-")
            .AssertOk("2001-01-01", "2001-12-31");
    }

    [TestMethod]
    public void TestIncompletes()
    {
        ct.Validate("2001-0")
            .AssertOk("2001-01-01", "2001-09-30");

        ct.Validate("2001-01-0")
            .AssertOk("2001-01-01", "2001-01-09");

        // not sure if we really want this semantic:

        ct.Validate("2001-1")
            .AssertOk("2001-10-01", "2001-12-31");

        ct.Validate("2001-2")
            .AssertFail();

        ct.Validate("2000-02-2")
            .AssertOk("2000-02-20", "2000-02-29");
    }

    [TestMethod]
    public void TestInvalid()
    {
        ct.Validate("2001-02-30").AssertFail();

        ct.Validate("0000").AssertFail();
        ct.Validate("2001-13").AssertFail();

        ct.Validate("-").AssertFail();
        ct.Validate("2-").AssertFail();
        ct.Validate("20-").AssertFail();
        ct.Validate("200-").AssertFail();

        ct.Validate("20000").AssertFail();
        ct.Validate("2000-1-").AssertFail();
        ct.Validate("2000-10-1-").AssertFail();
        ct.Validate("2000-001").AssertFail();
        ct.Validate("2000-01-001").AssertFail();
        ct.Validate("2000-01-01-").AssertFail();

        ct.Validate("2000-01-01 ").AssertFail();
    }

    [TestMethod]
    public void TestSubYearIntervals()
    {
        ct.Validate("2")
            .AssertOk("2001-01-01", "2999-12-31");

        ct.Validate("23")
            .AssertOk("2301-01-01", "2399-12-31");

        ct.Validate("234")
            .AssertOk("2341-01-01", "2349-12-31");
    }
}

public static class ColumnTypeExtensions
{
    public static ValidationResult Validate(this ColumnType ct, String value)
        => ct.Validate(null, value, IndexDirection.Unknown, default);

    public static void AssertFail(this ValidationResult result)
    {
        Assert.IsFalse(result.IsOk);
    }

    public static void AssertOk(this ValidationResult result, String lower, String? upper = null)
    {
        Assert.IsTrue(result.IsOk);
        Assert.AreEqual(lower, result.SqlLowerValue);
        Assert.AreEqual(upper, result.SqlUpperValue);
    }
}