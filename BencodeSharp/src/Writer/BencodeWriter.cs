using System.Collections;
using System.Numerics;
using System.Text;
using BencodeSharp.Exceptions;
using static BencodeSharp.Delimiter;

namespace BencodeSharp.Writer;

/// <summary>
/// Serializes an object to its bencode interpretation, and writes it to a stream
/// </summary>
public static class BencodeWriter
{
    private static readonly Encoding DefaultEncoding = Encoding.UTF8;
    /// <summary>
    /// Serializes an object to its bencode interpretation and writes it to a stream.
    /// Objects supported: uint, BigInteger, long, decimal, string, byte[], IList, IDictionary<string, object>.
    /// All other types are not supported.
    ///
    /// By default, the maximum depth is 10000 elements (e.g., to prevent
    /// recursive data structures from consuming all memory.) You can configure this in BencodeWriterOptions.
    /// </summary>
    /// <param name="output">The stream to write to.</param>
    /// <param name="input">The data to serialize.</param>
    /// <param name="options">The bencode writer options.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>An awaitable Task that serializes the object.</returns>
    public static async Task SerializeObjectAsync(Stream output, object input, BencodeWriterOptions? options = null,
        CancellationToken ct = default)
    {
        var currentDepth = 0;
        options ??= new BencodeWriterOptions();
        ct.ThrowIfCancellationRequested();
        var stack = new Stack<object>(new List<object>{ input });

        while (stack.TryPop(out var currentItem))
        {
            EnsureDepthIsWithinMax(options, currentDepth);

            var serializedItem = currentItem switch
            {
                // Integer types (uint, BigInteger, int, long, decimal) are serialized in the format "i<number>e".
                // Examples:
                // 123  ->  "i123e"
                // -234 ->  "i-234e"
                // 0    ->  "i0e"
                // 0.2  ->  (error) -- decimals are allowed for convenience
                //                     as long as they do not have a floating point portion (validated later)
                uint or BigInteger or int or long or decimal =>
                    SerializeNumber(currentItem),

                // String values are serialized as "<length>:<string>"
                // Examples:
                // "abc"            ->  "3:abc"
                // (empty string)   ->  "0:"
                string str =>
                    SerializeString(str),

                // Byte arrays are treated similarly to strings, serialized as "<length>:<content>"
                // The content of the byte array is converted to a string using default encoding.
                byte[] byteArray =>
                    SerializeByteArray(byteArray),

                // IList objects are serialized using the SerializeList method.
                // The method handles the serialization of lists by converting them into the appropriate format.
                // The format for lists is 'l<content>e' where <content> is the serialized elements of the list.
                // Examples:
                // List containing "abc", "abcd", "ab"   ->  "l3:abc4:abcd2:abe"
                // Nested list structure                 ->  "ll3:abcel4:abcdeli3ee"
                IList list =>
                    SerializeList(list, stack, ref currentDepth, ct),

                // IDictionary objects are serialized using the SerializeDictionary method.
                // This method handles the serialization of dictionaries by converting them into the appropriate bencode format.
                // In bencode, dictionaries are encoded as 'd<content>e', where <content> consists of alternating keys and their serialized values.
                // The keys are serialized as byte strings (like in the string case), and values are serialized based on their type.
                // Examples:
                // Dictionary with pairs {"key1": "abc", "key2": "def"}  -> "d4:key13:abc4:key23:defe"
                // Nested dictionary structure                           -> "d4:key1d3:sub4:itemee"
                IDictionary<string, object> dict =>
                    SerializeDictionary(dict, stack, ref currentDepth, ct),

                // The Tokens.End represents the end of a list, dictionary, or number.
                // It is serialized as "e".
                Tokens.End =>
                    DelimiterEnd(ref currentDepth),

                // If the current item is of an unsupported type, an exception is thrown.
                _ => throw new InvalidDataException($"Unsupported type: {currentItem.GetType()}")
            };

            await output.WriteAsync(serializedItem, ct);
        }
    }

    private static byte[] SerializeByteArray(byte[] byteArray)
    {
        return [.. DefaultEncoding.GetBytes($"{byteArray.Length}{ByteStringSeparator}"), ..byteArray];
    }

    private static byte[] SerializeString(string str)
    {
        return DefaultEncoding.GetBytes($"{str.Length}{ByteStringSeparator}{str}");
    }

    private static byte[] SerializeNumber(object? currentItem)
    {
        // ":R" for ToString needed because ToString() on BigInteger truncates to 50 digits by default
        return DefaultEncoding.GetBytes($"{NumberStart}{currentItem:R}{End}");
    }

    private static void EnsureDepthIsWithinMax(BencodeWriterOptions options, int currentDepth)
    {
        if (currentDepth > options.MaxDepth)
        {
            throw new BencodeOutOfRangeException(
                $"Stack depth {options.MaxDepth} exceeded. " +
                $"To change this, set the BencodeWriterOptions.MaxDepth property to a higher value.");
        }
    }
    private static byte[] DelimiterEnd(ref int currDepth)
    {
        currDepth--;
        return DefaultEncoding.GetBytes(End.ToString());
    }
    private static byte[] SerializeList(IList list, Stack<object> stack, ref int currDepth, CancellationToken ct)
    {
        currDepth++;
        stack.Push(new Tokens.End());
        for (var i = list.Count - 1; i >= 0; i--)
        {
            ct.ThrowIfCancellationRequested();
            if (list[i] == null) throw new BencodeInvalidDataException("List items cannot be null");
            
            stack.Push(list[i]!);
        }

        return DefaultEncoding.GetBytes(ListStart.ToString());
    }

    private static byte[] SerializeDictionary(IDictionary<string, object> dict, Stack<object> stack, ref int currDepth,
        CancellationToken ct)
    {
        currDepth++;
        stack.Push(new Tokens.End());
        foreach (var (key, value) in dict.Reverse())
        {
            ct.ThrowIfCancellationRequested();
            if (key == null) throw new BencodeInvalidDataException("Dictionary keys cannot be null");
            if (value == null) throw new BencodeInvalidDataException("Dictionary values cannot be null");
            
            stack.Push(value);
            stack.Push(key);
        }

        return DefaultEncoding.GetBytes(DictionaryStart.ToString());
    }
}