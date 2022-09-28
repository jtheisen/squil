using Humanizer;
using Squil.SchemaBuilding;

namespace Squil.PointsOfInterest
{
    public record PointOfInterest(String title, IEnumerable<String> items);

    public abstract class PointsOfInterestFinder
    {
        public virtual String Title => GetType().Name.Titleize();

        public abstract IEnumerable<String> Find(CMRoot root);
    }

    public abstract class Indexes : PointsOfInterestFinder
    {
        public override IEnumerable<String> Find(CMRoot root)
            => FindIndexes(root.GetAllIndexes()).Select(i => i.Name);

        protected abstract IEnumerable<CMIndexlike> FindIndexes(IEnumerable<CMIndexlike> indexes);
    }

    public abstract class ColumnInIndex : Indexes
    {
        protected abstract Boolean Check(CMDirectedColumn column);

        protected override IEnumerable<CMIndexlike> FindIndexes(IEnumerable<CMIndexlike> indexes) =>
            from i in indexes
            where i.IsSupported
            where i.Columns.Any(Check)
            select i;
    }

    public class UnsupportedIndexes : Indexes
    {
        protected override IEnumerable<CMIndexlike> FindIndexes(IEnumerable<CMIndexlike> indexes)
            => indexes.Where(i => !i.IsSupported);
    }

    public class DescendingIndexes : ColumnInIndex
    {
        protected override Boolean Check(CMDirectedColumn column)
            => column.d == IndexDirection.Desc;
    }

    public class NonCharNullIndexes : ColumnInIndex
    {
        protected override Boolean Check(CMDirectedColumn c)
            => c.c.Type is not CharacterColumnType && c.c.IsNullable;
    }

    public class DateTimeIndexes : ColumnInIndex
    {
        protected override Boolean Check(CMDirectedColumn c)
            => c.c.Type is DateOrTimeColumnType;
    }

    public class BitIndexes : ColumnInIndex
    {
        protected override Boolean Check(CMDirectedColumn c)
            => c.c.Type is IntegerColumnType ict && ict.IsBit;
    }

    public class IntegerIndexes : ColumnInIndex
    {
        protected override Boolean Check(CMDirectedColumn c)
            => c.c.Type is IntegerColumnType ict && !ict.IsBit;
    }

    public class DecimalIndexes : ColumnInIndex
    {
        protected override Boolean Check(CMDirectedColumn c)
            => c.c.Type is DecimalColumnType;
    }

    public class FloatIndexes : ColumnInIndex
    {
        protected override Boolean Check(CMDirectedColumn c)
            => c.c.Type is FloatColumnType ict;
    }

    public class LongestIndex : Indexes
    {
        protected override IEnumerable<CMIndexlike> FindIndexes(IEnumerable<CMIndexlike> indexes)
        {
            var list =
                from i in indexes
                group i by i.Columns.Length into g
                orderby g.Key descending
                select g.First();

            var candidate = list.FirstOrDefault();

            if (candidate != null)
            {
                yield return candidate;
            }
        }
    }

    public class IndexlessTables : PointsOfInterestFinder
    {
        public override IEnumerable<String> Find(CMRoot root)
        {
            var tables = root.RootTable.Relations.Values.Select(r => r.Table);

            return
                from t in tables
                where t.Indexes.Values.All(i => !i.IsSupported)
                select t.Name.Escaped;
        }
    }

    public class TablesWithOverlappingIndexes : PointsOfInterestFinder
    {
        public override IEnumerable<String> Find(CMRoot root)
        {
            var tables = root.RootTable.Relations.Values.Select(r => r.Table);

            return
                from t in tables
                let columns = (
                    from i in t.Indexes.Values
                    where i.IsSupported
                    from c in i.Columns
                    group (i, c) by c.c.Name into g
                    where g.Count() > 1
                    select g.Key
                ).FirstOrDefault()
                where columns != null
                select t.Name.Escaped;
        }
    }

    public class TablesWithIndexesWithKeyPrefix : PointsOfInterestFinder
    {
        public override IEnumerable<String> Find(CMRoot root)
        {
            var tables = root.RootTable.Relations.Values.Select(r => r.Table);

            return
                from t in tables
                let matchingIndexes = (
                    from k in t.ForeignKeys.Values
                    where k.Columns.Length > 0
                    from i in t.Indexes.Values.StartsWith(k.Columns.Select(c => c.Name.c), isPrefix: true)
                    select i
                )
                let m = matchingIndexes.FirstOrDefault()
                where m != null
                select t.Name.Escaped + "/" + m.Name;
        }
    }
}

namespace Squil
{
    using Squil.PointsOfInterest;

    public class PointsOfInterestManager : LazyStaticSingleton<PointsOfInterestManager>
    {
        List<PointsOfInterestFinder> finders;

        public PointsOfInterestManager()
        {
            finders = new List<PointsOfInterestFinder>();

            Add<UnsupportedIndexes>();
            Add<DescendingIndexes>();
            Add<NonCharNullIndexes>();
            Add<DateTimeIndexes>();
            Add<BitIndexes>();
            Add<IntegerIndexes>();
            Add<DecimalIndexes>();
            Add<FloatIndexes>();
            Add<LongestIndex>();
            Add<TablesWithOverlappingIndexes>();
            Add<TablesWithIndexesWithKeyPrefix>();
            Add<IndexlessTables>();
        }

        public IEnumerable<PointOfInterest> GetReport(CMRoot root)
        {
            return
                from f in finders
                select new PointOfInterest(f.Title, f.Find(root));
        }

        void Add<T>()
            where T : PointsOfInterestFinder, new()
        {
            finders.Add(new T());
        }
    }
}
