# BencodeSharp
 
A tap-water flavored non-recursive Bencode reader and writer written in C#. Designed to be boring and well-tested.

## Basic Usage for Reader
To use BencodeReader, first, create a stream containing your bencoded data. BencodeReader assumes all data is UTF-8 encoded. Then, use a static BencodeReader with this stream and call `Deserialize<T>` where `T` is the expected type of the decoded object (only `List<object>`, `SortedDictionary<string, object>`, `string`, `byte[]`, `int`, `decimal`, or `BigInteger`.) BencodeReader returns bytestrings instead of strings, so you'll have to manually convert them into strings. Again, designed to be boring: it doesn't know the type of data you're trying to parse and whether you want it as a string, or as bytes. _Yawn..._

```csharp
// Decoding a bencoded string
string bencodedString = "4:spam";
string decodedString = BencodeReader.Deserialize<string>(bencodedString);
// "spam"

// Decoding a bencoded integer
string bencodedInt = "i42e";
int decodedInt = BencodeReader.Deserialize<int>(bencodedInt);
// 42

// Decoding a bencoded list
string bencodedList = "l4:spam4:eggse";
List<object> decodedList = BencodeReader.Deserialize<List<object>>(bencodedList);
// ["spam", "eggs"] (returns byte[] by default, decode with Encoding.UTF8.GetString)

// Decoding a bencoded dictionary
string bencodedDict = "d3:cow3:moo4:spam4:eggse";
var decodedDict = BencodeReader.Deserialize<SortedDictionary<string, object>>(bencodedDict);
// {"cow": "moo", "spam": "eggs"} (returns byte[] by default for values, decode with Encoding.UTF8.GetString)
```

### Decoding Nested Structures
BencodeReader can also handle nested lists and dictionaries.

```csharp
var bencoded = "d4:city6:London4:listl4:spam4:eggse5:valuei42ee";
var decoded = BencodeReader.Deserialize<SortedDictionary<string, object>>(bencoded);
// {
//     {"city", "London"},
//     {"list", List<object> {"spam", "eggs"}},
//     {"value", 42}
// }

var bencoded = "ld3:key5:valueed4:name4:John4:yeari2021eee";
var decoded = BencodeReader.Deserialize<List<object>>(bencoded);
// {
//     Dictionary<string, object> {{"key", "value"}},
//     Dictionary<string, object> {{"name", "John"}}
//     Dictionary<string, object> {{"year", 2021}}
// }

```

Since BencodeSharp is non-recursive, you can decode very large objects. By default, the stack size is limited to 10000 elements, but you can change this.
```
const int stackSize = 10_000_000;
var input = string.Concat(Enumerable.Repeat("l", stackSize).Concat(Enumerable.Repeat("e", stackSize)));
BencodeReader.Deserialize<List<object>>(StringAsStream(input), new BencodeReaderOptions { MaxStackSize = stackSize });
```

...and as such, dictionaries can contain lists, lists can contain dictionaries and so on.

### Working with Streams
You can use BencodeReader with any stream, including file streams and memory streams.

```csharp
using (FileStream fileStream = new FileStream(pathToFile, FileMode.Open))
{
    // Assuming the file contains a bencoded dictionary
    var decodedData = BencodeReader.Deserialize<Dictionary<string, object>>(fileStream);

    // Handle the decoded data as needed
    // Example: print the contents if it's a dictionary
    foreach (var item in decodedData)
    {
        Console.WriteLine($"{item.Key}: {item.Value}");
    }
}
```

### Error Handling
BencodeReader will throw exceptions if it encounters malformed bencoded data or if the type being decoded does not match the actual type of the data.

```csharp
try
{
    string bencodedNegativeZero = "i-0e";
    int result = BencodeReader.Deserialize<int>(bencodedNegativeZero);
    Console.WriteLine($"Parsed value: {result}");
} catch (BencodeException) {
    // handle here
}
```

_Uh, oh, huh? you're still there. Oh, ok, thought you left. Let's continue._

The following are considered errors and will throw an exception:
- Dictionaries with the same key specified more than once.
- Bytestrings which are longer than `Int32.MaxValue` characters long (about 2GB.) This is a .NET limitation.
- Negative zero, or numbers prefixed with zero (assuming that it is not zero.)
- Numbers which are not integers (e.g., `-7.2`.) Only numbers that match `(-?)[0-9]+` are supported.
- Dictionary keys that are not byte-ordered. BencodeWriter will always byte-order dictionary keys.
- Nested structures deeper than the user-specified `maxStackSize`.
- Dictionary keys that are not bytestrings.
- The parser finished parsing, but there was more data to read. For example, `3:abcdefg` would throw an exception, because `defg` is not part of any bytestring.

The following are not considered errors, and will be parsed normally:
- Numbers longer than `Int32.MaxValue`. All numbers are parsed as a `BigInteger`, which has no upper bound, other than your computer's memory.
- Zero length strings.
- Deeply nested structures, as long as they are not greater than `MaxStackSize`.

Note: Dictionary keys are always parsed as bytestrings, then converted to strings.

## Basic Usage for Writer

### Serializing a Simple String
```csharp
var output = new MemoryStream();
await BencodeWriter.SerializeObjectAsync(output, "Hello, World!");
```
### Serializing an Integer
```csharp
var output = new MemoryStream();
await BencodeWriter.SerializeObjectAsync(output, 12345);
```

### Serializing a List
```csharp
var myList = new List<object> { "spam", "eggs", 42 };
var output = new MemoryStream();
await BencodeWriter.SerializeObjectAsync(output, myList);
```

### Serializing a Dictionary
```csharp
var myDict = new Dictionary<string, object>
{
    ["name"] = "John Doe",
    ["age"] = 30
};
var output = new MemoryStream();
await BencodeWriter.SerializeObjectAsync(output, myDict);
```

### Serializing a Complex Structure (Nested Lists and Dictionaries)
```csharp
var complexData = new List<object>
{
    new Dictionary<string, object>
    {
        ["id"] = 1,
        ["info"] = new Dictionary<string, object>
        {
            ["name"] = "Nested Name",
            ["value"] = 100
        }
    },
    new List<object> { "list item 1", 200 }
};
var output = new MemoryStream();
await BencodeWriter.SerializeObjectAsync(output, complexData);
```

## Async Support
BencodeReader does not inherently support asynchronous operations since it operates synchronously on the provided Stream. If your stream supports asynchronous operations, you should manage asynchronous reads from the stream before passing the data to BencodeReader.

## Why is non-recursiveness important?
BencodeSharp is non-recursive. This means that you can parse arbitrary deeply-nested structures--as deep as your computer's memory allows. Many Bencode parsers that exist are recursive, which means that they can throw a stack overflow exception if the structures are nested too deeply. Stack overflow exceptions are usually not-user-handleable, which means they will crash your program. By default, BencodeSharp limits the depth to 10000, but you can change this in the options.

## What does "tap-water" flavored mean?

Tap-water flavored is a way of saying boring, or average. BencodeSharp should be predictable, have no surprises (such as stack overflows), shouldn't try to be super smart, but instead just be basic, return lists, dictionaries, bytestrings, and numbers of arbitrary length and let the consumer decide how it should be ultimately formatted. BencodeSharp assumes that data is encoded as UTF-8, but this might be configurable in the future. It should be well-tested, that is, there shouldn't be any surprises (oh! how come this doesn't work?) It is cross-tested with BencodeNET on 3000 private test cases, and also cross-tested with itself (BencodeReader should be able to read the output from BencodeWriter, and they should be identical.)

The Bencode specification is ambigious, which means that many parsers that exist today parse data differently, and may or may not return errors in different situations. For example, some parsers may return errors or continue parsing data in these situations:

- Dictionaries with the same key multiple times (some parsers will throw an error, some will use the last key)
- Numbers such as "3+E2" are converted into "300", some parsers throw an error.
- Negative zero is converted into 0 sometimes, or throws an error.
- Decimals are sometimes supported, sometimes not.
- Some numbers are parsed as ints, which means there is a hard limit at `Int32.Max` for numeric values. The Bencode specification says that numbers should have no arbitrary limits, however.
- Sometimes, zero length strings are not allowed.
- If the input ends (e.g., `1:a2:abc`), the `c` is not part of any string. Some parsers will just stop parsing and ignore the rest, some will throw an error.

## How to run fuzz tests

BencodeSharp uses sharpfuzz for fuzz testing. The fuzz testing project is called "BencodeSharp.Fuzz". You can set up sharpfuzz here: https://github.com/Metalnem/sharpfuzz?tab=readme-ov-file#installation
