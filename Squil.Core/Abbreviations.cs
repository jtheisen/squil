using Humanizer;

namespace Squil;

public class Abbreviator
{
    public Dictionary<ObjectName, String> Calculate(ObjectName[] names)
    {
        var result = new Dictionary<ObjectName, String>();

        var existingAbbriviation = new HashSet<String>();

        var suggestions = names.Select(n => n.LastPart).Select(Suggest).Select(e => e.GetEnumerator()).ToArray();

        var namesDone = 0;

        while (namesDone < names.Length)
        {
            for (var i = 0; i < names.Length; ++i)
            {
                if (suggestions[i] == null) continue;

                if (!suggestions[i].MoveNext())
                {
                    suggestions[i] = null;

                    ++namesDone;

                    continue;
                }

                var suggestion = suggestions[i].Current;

                if (suggestion != null && !existingAbbriviation.Contains(suggestion))
                {
                    result[names[i]] = suggestion;
                    existingAbbriviation.Add(suggestion);

                    suggestions[i] = null;

                    ++namesDone;
                }
            }
        }

        return result;
    }

    IEnumerable<String> Suggest(String name)
    {
        if (name.Length == 0) yield break;

        var capitals = name.Humanize(LetterCasing.Title).Where(c => Char.IsUpper(c)).ToArray();

        yield return capitals.Length >= 2 ? new String(capitals.Take(2).ToArray()) : null;

        name = name.ToUpper();

        yield return name.Substring(0, Math.Min(name.Length, 2));

        var trailConsonants = name.Skip(1).Where(c => !IsVowel(c)).ToArray();

        var firstLetter = new String(name[0], 1);

        foreach (var c in trailConsonants.Reverse())
        {
            yield return firstLetter + c;
        }
    }

    const String vowels = "euioa";

    static Boolean IsVowel(Char c)
    {
        return vowels.Contains(c);
    }
}
