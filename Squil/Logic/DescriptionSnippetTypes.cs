using Squil.SchemaBuilding;
using Squil.Shared.DescriptionSnippets;

namespace Squil;

public class DescriptionSnippetTypeRegistry : LazyStaticSingleton<DescriptionSnippetTypeRegistry>
{
    Dictionary<String, Type> types;

    public Type Get(String name) => types.Get(name, $"Can't find description snippet {name}");

    public DescriptionSnippetTypeRegistry()
    {
        this.types = new Dictionary<String, Type>();

        types.Add("StackOverflow", typeof(StackOverflow));
    }
}
