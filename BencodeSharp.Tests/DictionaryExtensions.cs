using System.Collections;

namespace BencodeSharp.Tests;

public static class DictionaryExtensions
{
    public static bool AreObjectsEqual(object? obj1, object? obj2)
    {
        if (ReferenceEquals(obj1, obj2)) return true;
        if (obj1 == null || obj2 == null) return false;

        if (obj1 is IDictionary dict1 && obj2 is IDictionary dict2)
            return AreDictionariesEqual(dict1, dict2);

        if (obj1 is IList list1 && obj2 is IList list2)
            return AreListsEqual(list1, list2);

        return Equals(obj1, obj2);
    }

    private static bool AreDictionariesEqual(IDictionary dict1, IDictionary dict2)
    {
        if (dict1.Count != dict2.Count) return false;

        foreach (DictionaryEntry kvp in dict1)
        {
            if (!dict2.Contains(kvp.Key))
                return false;

            if (!AreObjectsEqual(kvp.Value, dict2[kvp.Key]))
                return false;
        }

        return true;
    }

    private static bool AreListsEqual(IList list1, IList list2)
    {
        if (list1.Count != list2.Count) return false;

        for (int i = 0; i < list1.Count; i++)
        {
            if (!AreObjectsEqual(list1[i], list2[i]))
                return false;
        }

        return true;
    }
}