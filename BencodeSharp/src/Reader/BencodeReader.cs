using System.Collections;
using BencodeSharp.Exceptions;
using System.Numerics;
using System.Text;

namespace BencodeSharp.Reader;

/// <summary>
///     Strings are length-prefixed base ten followed by a colon and the string. For
///     example 4:spam corresponds to 'spam'. Integers are represented by an 'i' followed by
///     the number in base 10 followed by an 'e'. For example i3e corresponds to 3 and
///     i-3e corresponds to -3. Integers have no size limitation. i-0e is invalid. All
///     encodings with a leading zero, such as i03e, are invalid, other than i0e, which of
///     course corresponds to 0. Lists are encoded as an 'l' followed by their elements (also
///     bencoded) followed by an 'e'. For example l4:spam4:eggse corresponds to ['spam',
///     'eggs']. Dictionaries are encoded as a 'd' followed by a list of alternating keys and
///     their corresponding values followed by an 'e'. For example, d3:cow3:moo4:spam4:eggse
///     corresponds to {'cow': 'moo', 'spam': 'eggs'} and d4:spaml1:a1:bee corresponds to {'spam':
///     ['a', 'b']}. Keys must be strings and appear in sorted order (sorted as raw strings,
///     not alphanumerics).
///     https://www.bittorrent.org/beps/bep_0003.html
/// </summary>
public static class BencodeReader
{
    internal static readonly Encoding DefaultEncoding = Encoding.UTF8;

    // ReSharper disable once UnusedMember.Global
    public static T Deserialize<T>(string input, BencodeReaderOptions? options = null, CancellationToken ct = new())
    {
        return Deserialize<T>(new MemoryStream(DefaultEncoding.GetBytes(input)), options, ct);
    }

    /// <summary>
    /// Decodes a bencoded stream into an object.
    /// </summary>
    /// <typeparam name="T">The type to deserialize to. Note that only lists, dictionaries, strings, and numbers are supported.</typeparam>
    /// <param name="inputStream">The input stream.</param>
    /// <param name="options">The options.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A new object representing the decoded data.</returns>
    /// <exception cref="ArgumentException">Thrown when the type T is not castable.</exception>
    /// <exception cref="BencodeInvalidCastException">Thrown when the parsed data cannot be cast to the desired type.</exception>
    /// <exception cref="BencodeOutOfRangeException">Thrown when the stack size exceeds the maximum allowed limit or the bytestring length exceeds maximum allowed limit.</exception>
    /// <exception cref="BencodeInvalidDataException">Thrown when there is invalid data in the bencoded input.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is canceled through the cancellation token.</exception>
    public static T Deserialize<T>(Stream inputStream, BencodeReaderOptions? options = null, CancellationToken ct = new())
    {
        ct.ThrowIfCancellationRequested();

        IStreamReader baseReader = StreamReader.Create(inputStream);
        options ??= new BencodeReaderOptions();

        var parsedData = ParseInternal(baseReader, options, ct);

        return TryConvertParsedData<T>(parsedData) ??
               throw new BencodeInvalidCastException($"Type ${typeof(T)} not castable");
    }

    private static T TryConvertParsedData<T>(object parsedData)
    {
        return parsedData switch
        {
            byte[] byteArray when typeof(T) == typeof(string) => (T)(dynamic)DefaultEncoding.GetString(byteArray),
            BigInteger bigInt when typeof(T) == typeof(int)   => (T)(dynamic)int.Parse(bigInt.ToString("R")),
            _ => (T)parsedData
        };
    }


    private static object ParseInternal(IStreamReader baseReader,
        BencodeReaderOptions options,
        CancellationToken ct)
    {
        var stack = new Stack<object>();

        while (baseReader.TryPeek<char>(out _))
        {
            ct.ThrowIfCancellationRequested();
            EnsureStackSizeWithinLimit(stack, options.MaxStackSize);

            var parsedItem = ParseItem(baseReader, stack, options, ct);
            stack.Push(parsedItem);
        }

        ValidateFinalStackState(stack);

        return options.AllowUnwrappedElements ? stack.First() : stack;
    }

    private static void EnsureStackSizeWithinLimit(ICollection stack, int maxStackSize)
    {
        if (stack.Count > maxStackSize)
            throw new BencodeOutOfRangeException($"Stack size exceeded the maximum allowed limit of {maxStackSize}.");
    }

    private static object ParseItem(IStreamReader baseReader, Stack<object> stack,
        BencodeReaderOptions options,
        CancellationToken ct)
    {
        // this is guaranteed* to be a char because ParseInternal already TryPeek'd it
        var currChar = (char)baseReader.PeekChar();

        return currChar switch
        {
            >= '0' and <= '9'         => ReadLengthPrefixedByteString(baseReader, ct),
            Delimiter.DictionaryStart => GetDictionaryStart(baseReader),
            Delimiter.ListStart       => GetListStart(baseReader),
            Delimiter.NumberStart     => ReadBigInteger(baseReader, ct),
            Delimiter.End             => ReadDictOrListEnd(baseReader, stack, options, ct),
            _                         => UnknownType(baseReader, currChar)
        };
    }

    private static object UnknownType(IStreamReader baseReader, char currChar)
    {
        throw new BencodeInvalidDataException($"Invalid start character in string: '{currChar}'", baseReader.GetPosition());
    }

    private static void ValidateFinalStackState(Stack<object> stack)
    {
        if (stack.Count != 1)
            throw new BencodeInvalidDataException($"Unexpected number of elements in stack: {stack.Count}. Expected 1.");

        if (stack.Peek() is Tokens.StartToken)
            throw new BencodeInvalidDataException("Malformed input detected: start token found at the end of parsing.");
    }

    private static Tokens.DictionaryStart GetDictionaryStart(IStreamReader baseReader)
    {
        baseReader.TakeThisOrThrow(Delimiter.DictionaryStart);
        return new Tokens.DictionaryStart();
    }

    private static Tokens.ListStart GetListStart(IStreamReader baseReader)
    {
        baseReader.TakeThisOrThrow(Delimiter.ListStart);
        return new Tokens.ListStart();
    }

    private static object ReadDictOrListEnd(IStreamReader baseReader, Stack<object> stack,
        BencodeReaderOptions options, CancellationToken ct)
    {
        baseReader.TakeThisOrThrow(Delimiter.End);

        var items = new List<object>();
        while (stack.TryPeek(out var item) && item is not Tokens.StartToken)
        {
            ct.ThrowIfCancellationRequested();
            items.Insert(0, stack.Pop());
        }

        // expecting a start token
        if (!stack.TryPop(out var type) || type is not Tokens.StartToken)
            throw new BencodeInvalidDataException("Expected a start token in the stack.");

        return type switch
        {
            Tokens.ListStart => items,
            Tokens.DictionaryStart => ReadDictionary(items, options, ct),
            _ => throw new BencodeInvalidDataException($"The data type {type.GetType()} is unsupported")
        };
    }

    private static BigInteger ReadBigInteger(IStreamReader baseReader, CancellationToken ct)
    {
        baseReader.TakeThisOrThrow(Delimiter.NumberStart);
        var parsedBigInteger = ReadBigIntegerRaw(baseReader, ct);
        baseReader.TakeThisOrThrow(Delimiter.End);

        return parsedBigInteger;
    }

    private static IDictionary<string, object> ReadDictionary(IEnumerable<object> keyValuePairs,
        BencodeReaderOptions options, CancellationToken ct)
    {
        var dict = new SortedDictionary<string, object>(StringComparer.Ordinal);
        string? prevKey = null;
        foreach (var keyValuePair in keyValuePairs.Chunk(2))
        {
            ct.ThrowIfCancellationRequested();
            if (keyValuePair.Length != 2)
                throw new BencodeInvalidDataException($"Expected value for key '{keyValuePair[0]}', did not receive one");

            var (key, value) = (keyValuePair[0], keyValuePair[1]);

            // all dictionary keys _must_ be strings
            if (key is not byte[] byteStr) throw new BencodeInvalidDataException("Keys must be bytestrings");
            var keyStr = DefaultEncoding.GetString(byteStr);

            // all keys must be sorted
            if (prevKey != null && !options.AllowUnorderedKeys &&
                string.Compare(prevKey, keyStr, StringComparison.Ordinal) > 0)
                throw new BencodeInvalidDataException("Dictionary keys were not sorted");

            if (!dict.TryAdd(keyStr, value)) throw new BencodeInvalidDataException($"Key {keyStr} already exists");

            prevKey = keyStr;
        }

        return dict;
    }

    private static BigInteger ReadBigIntegerRaw(IStreamReader baseReader, CancellationToken ct)
    {
        var numberPieces = baseReader.TakeWhile<char>(piece => char.IsAsciiDigit(piece) || piece == '-', ct);
        if (numberPieces is (['0', ..] or ['-', '0', ..]) and not ['0'])
            throw new BencodeInvalidDataException("Numbers cannot begin with zero and cannot be negative zero", baseReader.GetPosition());

        ct.ThrowIfCancellationRequested();
        if (BigInteger.TryParse(string.Concat(numberPieces), out var parsedInteger)) return parsedInteger;

        throw new BencodeInvalidDataException($"Expected number, got: {string.Concat(numberPieces)}", baseReader.GetPosition());
    }

    private static byte[] ReadLengthPrefixedByteString(IStreamReader baseReader, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var byteStringLength = ReadBigIntegerRaw(baseReader, ct);
        if (byteStringLength < 0)
            throw new BencodeInvalidDataException($"Bytestring length cannot be negative, got: '{byteStringLength}'", baseReader.GetPosition());

        if (byteStringLength > int.MaxValue)
            throw new BencodeOutOfRangeException($"Bytestring length exceeds maximum allowed limit of {int.MaxValue} characters.");

        baseReader.TakeThisOrThrow(Delimiter.ByteStringSeparator);
        return baseReader.TakeBytes((int)byteStringLength);
    }
}