using BencodeSharp.Reader;

namespace BencodeSharp;

internal static class Extensions
{
    private const int Eof = -1;

    private static void EnsureTypesEqual(Type firstType, Type secondType, string operation)
    {
        if (firstType != secondType) throw new ArgumentException($"Only char types are supported for {operation}");
    }

    public static List<T> TakeWhile<T>(this IStreamReader reader, Func<T, bool> condition, CancellationToken ct = new())
        where T : struct
    {
        EnsureTypesEqual(typeof(T), typeof(char), "TakeWhile");

        var list = new List<T>();
        while (reader.TryPeek(out var c) && condition((T)(object)c!.Value))
        {
            ct.ThrowIfCancellationRequested();
            list.Add((T)(object)c);
            _ = reader.ReadChar();
        }

        return list;
    }

    public static bool TryPeek<T>(this IStreamReader reader, out T? value) where T : struct
    {
        EnsureTypesEqual(typeof(T), typeof(char), "TryPeek");

        value = default;

        var peekedResult = reader.PeekChar();
        if (peekedResult == Eof) return false;

        value = (T?)(object)(char)peekedResult;
        return true;
    }

    private static T Take<T>(this IStreamReader reader) where T : struct
    {
        EnsureTypesEqual(typeof(T), typeof(char), "Take");

        return (T)(object)reader.ReadChar();
    }

    public static byte[] TakeBytes(this IStreamReader reader, int length)
    {
        var bytesRead = reader.ReadBytesUnsafe(length);
        if (bytesRead.Length != length) throw new InvalidOperationException($"Requested to read {length} bytes, only read {bytesRead.Length}");
        return bytesRead;
    }

    public static void TakeThisOrThrow<T>(this IStreamReader reader, T item) where T : struct
    {
        EnsureTypesEqual(typeof(T), typeof(char), "Take");

        if (reader.Take<char>() != (char)(object)item)
            throw new ArgumentException(nameof(item), $"Expected: '{(char)(object)item}'");
    }
}