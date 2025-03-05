using System.Reflection;

namespace SIL.Transcriber.Utility.Extensions;
public static class ObjectExtensions
{
    public static IEnumerable<T> AsEnumerable<T>(this T element)
    {
        yield return element;
    }

    public static T[] AsArray<T>(this T element)
    {
        return
        [
            element
        ];
    }

    public static List<T> AsList<T>(this T element)
    {
        return
        [
            element
        ];
    }

    public static HashSet<T> AsHashSet<T>(this T element)
    {
        return
        [
            element
        ];
    }

    public static void CopyProperties<T>(this T source, T destination)
    {
        if (source == null || destination == null)
            throw new ArgumentNullException(source == null ? "source" : "destination");

        Type type = typeof(T);
        PropertyInfo[] properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (PropertyInfo property in properties)
        {
            if (property.Name != "Id" && property.Name != "StringId" && property.CanRead && property.CanWrite)
            {
                object? value = property.GetValue(source);
                property.SetValue(destination, value);
            }
        }
    }

}
