using System.Numerics;
using System.Text;
using BencodeNET.Objects;
using BencodeNET.Parsing;
using BencodeSharp.Exceptions;
using BencodeSharp.Reader;
using BencodeSharp.Writer;
using Microsoft.IO;

namespace BencodeSharp.Tests;

[TestClass]
public class BencodeReaderTests
{
    [DataTestMethod]
    [DataRow("7:abcdefg", "abcdefg")]
    [DataRow("4:spam", "spam")]
    [DataRow("0:", "")]
    [DataRow("1: ", " ")]
    [DataRow("14:Hello, \u4e16\u754c!", "Hello, \u4e16\u754c!")]
    public void DecodeString_ValidInput_ReturnsString(string input, string expected)
    {
        // Act
        var result = BencodeReader.Deserialize<string>(StringAsStream(input));

        // Assert
        Assert.AreEqual(expected, result);
    }

    // Test decoding a string
    [TestMethod]
    public void DecodeBytes_ValidInput_ReturnsString()
    {
        RecyclableMemoryStreamManager manager = new RecyclableMemoryStreamManager();
        // Arrange
        for (var i = 0; i < byte.MaxValue; i++)
        {
            for (var j = 0; j < byte.MaxValue; j++)
            {
                for (var k = 0; k < byte.MaxValue; k++)
                {
                    for (var l = 0; l < 5; l++)
                    {
                        var input = new[] { (byte)i, (byte)j, (byte)k, (byte)l };
                        using var stream = manager.GetStream([.. "4:"u8.ToArray(), .. input]);

                        // Act
                        var result = BencodeReader.Deserialize<byte[]>(stream);

                        // Assert
                        CollectionAssert.AreEqual(input, result);
                    }
                }
            }
        }
    }

    // Test decoding an integer
    [TestMethod]
    public void DecodeInteger_ValidInput_ReturnsInteger()
    {
        // Arrange
        const string input = "i42e";

        // Act
        var result = BencodeReader.Deserialize<int>(StringAsStream(input));

        // Assert
        Assert.AreEqual(42, result);
    }

    // Test decoding a BigInteger
    [TestMethod]
    public void DecodeBigInteger_ValidInput_ReturnsBigInteger()
    {
        // Arrange
        const string input = "123456789012345678901234567890123456789012345678901234567" +
                             "89012345678901234567890123456789012345678901234567890123456" +
                             "7890123456789012345678901234567890123456789012345678901234567890";

        // Act
        var result = BencodeReader.Deserialize<BigInteger>(StringAsStream($"i{input}e"));

        // Assert
        Assert.AreEqual(BigInteger.Parse(input), result);
    }

    // Test decoding a list
    [TestMethod]
    public void DecodeList_ValidInput_ReturnsList()
    {
        // Arrange
        const string input = "l4:spam4:eggse";

        // Act
        var result = BencodeReader.Deserialize<List<object>>(StringAsStream(input)).Select(l => (byte[])l).ToList();

        // Assert
        Assert.IsTrue(result.Count == 2);
        CollectionAssert.AreEqual("spam"u8.ToArray(), result[0]);
        CollectionAssert.AreEqual("eggs"u8.ToArray(), result[1]);
    }

    // Test decoding a dictionary
    [TestMethod]
    public void DecodeDictionary_ValidInput_ReturnsDictionary()
    {
        // Arrange
        const string input = "d3:cow3:moo4:spam4:eggse";

        // Act
        var result = BencodeReader.Deserialize<SortedDictionary<string, object>>(StringAsStream(input));

        // Assert
        Assert.AreEqual(2, result.Count);
        CollectionAssert.AreEqual("moo"u8.ToArray(), (byte[])result["cow"]);
        CollectionAssert.AreEqual("eggs"u8.ToArray(), (byte[])result["spam"]);
    }

    // Test decoding a very, very long list. Main goal is that there should not be a stackoverflow because
    // the reader is not recursive
    [TestMethod]
    public void DecodeList_ValidInput_ReturnsLongList()
    {
        // Arrange
        const int stackSize = 10_000_000;
        var input = string.Concat(Enumerable.Repeat("l", stackSize).Concat(Enumerable.Repeat("e", stackSize)));
        
        // Act
        var result = BencodeReader.Deserialize<List<object>>(StringAsStream(input), new BencodeReaderOptions { MaxStackSize = stackSize });

        // Assert
        Assert.AreEqual(1, result.Count);
    }

    // Test decoding an invalid input (e.g., leading zero)
    [TestMethod]
    [ExpectedException(typeof(BencodeInvalidDataException))]
    public void DecodeInteger_InvalidInputLeadingZero_ThrowsFormatException()
    {
        // Arrange
        const string input = "i03e";

        // Act
        BencodeReader.Deserialize<int>(StringAsStream(input));
    }

    // Test decoding an invalid input (e.g., leading zero)
    [TestMethod]
    [ExpectedException(typeof(BencodeInvalidDataException))]
    public void DecodeInteger_InvalidPlus_ThrowsFormatException()
    {
        // Arrange
        const string input = "i3e+2e";

        // Act
        BencodeReader.Deserialize<int>(StringAsStream(input));
    }

    [Ignore]
    [TestMethod]
    public void DecodeTorrentFiles()
    {
        // Arrange
        foreach (var file in Directory.EnumerateFiles(@"C:\dataset", "*"))
        {
            using var fs = new FileStream(file, FileMode.Open, FileAccess.Read);
            var bencodeNetParser = new BencodeParser();

            // Act
            var parsedTorrent = BencodeReader.Deserialize<SortedDictionary<string, object>>(fs);
            fs.Position = 0;
            var bdictionary = bencodeNetParser.Parse<BDictionary>(fs);
            var asStringDict = ConvertDictionary(parsedTorrent);
            var bencodeDictAsDict = ConvertDictionaryNet(bdictionary.ToDictionary());

            if (!DictionaryExtensions.AreObjectsEqual(asStringDict, bencodeDictAsDict))
            {
                throw new Exception(file);
            }
        }
    }

    [Ignore]
    [TestMethod]
    public async Task DecodeEncodeTorrentFiles()
    {
        // Arrange
        foreach (var file in Directory.EnumerateFiles(@"C:\dataset", "*"))
        {
            using var actual = new MemoryStream();
            using var expected = new MemoryStream();
            await using var fs = new FileStream(file, FileMode.Open, FileAccess.Read);
            await fs.CopyToAsync(expected);
            fs.Position = 0;

            // Act
            var parsedTorrent = BencodeReader.Deserialize<SortedDictionary<string, object>>(fs);
            await BencodeWriter.SerializeObjectAsync(actual, parsedTorrent);
            CollectionAssert.AreEqual(actual.ToArray(), expected.ToArray());
        }
    }

    private Dictionary<string, object> ConvertDictionaryNet(Dictionary<BString, IBObject> dict)
    {
        var toReturn = new Dictionary<string, object>();
        foreach (var (key, value) in dict)
        {
            toReturn[key.ToString()] = value switch
            {
                BString valueStr => valueStr.ToString(),
                BNumber bigInt => bigInt.ToString(),
                BDictionary sortedDict => ConvertDictionaryNet(sortedDict.ToDictionary()),
                BList objList => ConvertListNet(objList.ToList()),
                _ => throw new Exception(value.GetType().ToString())
            };
        }

        return toReturn;
    }

    private List<object> ConvertListNet(IList<IBObject> objList)
    {
        var toReturn = new List<object>();

        foreach (var item in objList)
        {
            switch (item)
            {
                case BString itemStr:
                    toReturn.Add(itemStr.ToString());
                    break;
                case BNumber itemBigInt:
                    toReturn.Add(itemBigInt.ToString());
                    break;
                case IList<IBObject> itemIList:
                {
                    toReturn.AddRange(ConvertListNet(itemIList));
                    break;
                }
                case BDictionary dict:
                    toReturn.Add(ConvertDictionaryNet(dict.ToDictionary()));
                    break;
                default:
                    throw new Exception(item.GetType().ToString());
            }
        }

        return toReturn;
    }

    private MemoryStream StringAsStream(string input)
    {
        return new MemoryStream(Encoding.UTF8.GetBytes(input));
    }

    private Dictionary<string, object> ConvertDictionary(SortedDictionary<string, object> dict)
    {
        var toReturn = new Dictionary<string, object>();
        foreach (var (key, value) in dict)
        {
            toReturn[key] = value switch
            {
                string valueStr => valueStr,
                BigInteger bigInt => bigInt.ToString(),
                SortedDictionary<string, object> sortedDict => ConvertDictionary(sortedDict),
                byte[] byteList => Encoding.UTF8.GetString(byteList),
                IList<object> objList => ConvertList(objList),
                _ => throw new Exception(value.GetType().ToString())
            };
        }

        return toReturn;
    }

    private List<object> ConvertList(IEnumerable<object> objList)
    {
        var toReturn = new List<object>();

        foreach (var item in objList)
        {
            switch (item)
            {
                case string itemStr:
                    toReturn.Add(itemStr);
                    break;
                case BigInteger itemBigInt:
                    toReturn.Add(itemBigInt.ToString());
                    break;
                case IList<object> itemIList:
                {
                    toReturn.AddRange(ConvertList(itemIList));
                    break;
                }
                case byte[] bytes:
                    toReturn.Add(Encoding.UTF8.GetString(bytes));
                    break;
                case SortedDictionary<string, object> dict:
                    toReturn.Add(ConvertDictionary(dict));
                    break;
                default:
                    throw new Exception(item.GetType().ToString());
            }
        }

        return toReturn;
    }
}