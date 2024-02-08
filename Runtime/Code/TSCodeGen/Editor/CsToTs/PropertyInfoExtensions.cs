using System.Linq;
using System.Reflection;

public static class PropertyInfoExtensions
{
    public static bool IsStatic(this PropertyInfo source, bool nonPublic = false)
        => source.GetAccessors(nonPublic).Any(x => x.IsStatic);
}