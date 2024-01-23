using System.Collections;
using System.Numerics;
using System.Text;
using BencodeSharp.Exceptions;
using BencodeSharp.Reader;
using BencodeSharp.Writer;
using KellermanSoftware.CompareNetObjects;
using KellermanSoftware.CompareNetObjects.Reports;
using Microsoft.VisualBasic;

namespace BencodeSharp.Tests;

[TestClass]
public class BencodeWriterTests
{
    [TestMethod]
    public async Task SerializeObjectAsync_String_WritesCorrectBencode()
    {
        // Arrange
        const string input = "spam";
        var memoryStream = new MemoryStream();

        // Act
        await BencodeWriter.SerializeObjectAsync(memoryStream, input);
        var result = memoryStream.ToArray();

        // Assert
        CollectionAssert.AreEqual("4:spam"u8.ToArray(), result);
    }

    [TestMethod]
    public async Task SerializeObjectAsync_Integer_WritesCorrectBencode()
    {
        // Arrange
        const int input = 42;
        var memoryStream = new MemoryStream();

        // Act
        await BencodeWriter.SerializeObjectAsync(memoryStream, input);
        var result = memoryStream.ToArray();

        // Assert
        CollectionAssert.AreEqual("i42e"u8.ToArray(), result);
    }

    [TestMethod]
    public async Task SerializeObjectAsync_BigInteger_WritesCorrectBencode()
    {
        // Arrange
        var bigIntStr = string.Concat(Enumerable.Repeat("9", 100));
        var input = BigInteger.Parse(bigIntStr);
        var memoryStream = new MemoryStream();

        // Act
        await BencodeWriter.SerializeObjectAsync(memoryStream, input);
        var result = memoryStream.ToArray();

        // Assert
        CollectionAssert.AreEqual(Encoding.UTF8.GetBytes($"i{bigIntStr}e"), result);
    }

    [TestMethod]
    public async Task SerializeObjectAsync_List_WritesCorrectBencode()
    {
        // Arrange
        var input = new List<object> { "spam", "eggs" };
        var memoryStream = new MemoryStream();
        
        // Act
        await BencodeWriter.SerializeObjectAsync(memoryStream, input);
        var result = memoryStream.ToArray();

        // Assert
        CollectionAssert.AreEqual("l4:spam4:eggse"u8.ToArray(), result);
    }

    [TestMethod]
    public async Task SerializeObjectAsync_Dictionary_WritesCorrectBencode()
    {
        // Arrange
        var input = new Dictionary<string, object>
        {
            ["cow"] = "moo",
            ["spam"] = "eggs"
        };
        var memoryStream = new MemoryStream();

        // Act
        await BencodeWriter.SerializeObjectAsync(memoryStream, input);
        var result = memoryStream.ToArray();

        // Assert
        CollectionAssert.AreEqual("d3:cow3:moo4:spam4:eggse"u8.ToArray(), result);
    }

    [TestMethod]
    public async Task SerializeObjectAsync_ByteArray_WritesCorrectBencode()
    {
        // Arrange
        var input = "spam"u8.ToArray();
        var memoryStream = new MemoryStream();

        // Act
        await BencodeWriter.SerializeObjectAsync(memoryStream, input);
        var result = memoryStream.ToArray();

        // Assert
        CollectionAssert.AreEqual("4:spam"u8.ToArray(), result);
    }

    [TestMethod]
    public async Task SerializeObjectAsync_NestedList_WritesCorrectBencode()
    {
        // Arrange
        var input = new List<object> { "list", new List<object> { "nested", 42 } };
        var memoryStream = new MemoryStream();

        // Act
        await BencodeWriter.SerializeObjectAsync(memoryStream, input);
        var result = memoryStream.ToArray();

        // Assert
        CollectionAssert.AreEqual("l4:listl6:nestedi42eee"u8.ToArray(), result);
    }

    [TestMethod]
    public async Task SerializeObjectAsync_NestedDictionary_WritesCorrectBencode()
    {
        // Arrange
        var input = new Dictionary<string, object>
        {
            ["dict"] = new Dictionary<string, object>
            {
                ["nested"] = "value",
                ["number"] = 123
            }
        };
        var memoryStream = new MemoryStream();
        
        // Act
        await BencodeWriter.SerializeObjectAsync(memoryStream, input);
        var result = memoryStream.ToArray();

        // Assert
        CollectionAssert.AreEqual("d4:dictd6:nested5:value6:numberi123eee"u8.ToArray(), result);
    }

    [TestMethod]
    public async Task SerializeObjectAsync_RandomObject_WritesCorrectBencode()
    {
        // test ensures that BencodeWriter doesn't throw an exception on large inputs
        for (int i = 0; i < 100; i++)
        {
            // Arrange
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var input = RandomObjectCreator.GenerateRandomObject(10, cts.Token);

            using var memoryStream = new MemoryStream();

            // Act
            await BencodeWriter.SerializeObjectAsync(memoryStream, input, ct: cts.Token);
            memoryStream.Position = 0;

            var logic = new CompareLogic();

            switch (input)
            {
                case SortedDictionary<string, object>:
                {
                    var deserialized = BencodeReader.Deserialize<SortedDictionary<string, object>>(memoryStream);
                    Assert.IsTrue(logic.Compare(deserialized, input).AreEqual);
                    break;
                }
                case IList<object>:
                {
                    var deserialized = BencodeReader.Deserialize<IList<object>>(memoryStream);
                    Assert.IsTrue(logic.Compare(deserialized, input).AreEqual);
                    break;
                }
                case BigInteger num:
                    Assert.AreEqual(input, num);
                    break;
                case byte[] bytes:
                {
                    var deserialized = BencodeReader.Deserialize<byte[]>(memoryStream);
                    CollectionAssert.AreEqual(bytes, deserialized);
                    break;
                }
                default:
                    throw new Exception("Test case was not handled");
            }
        }
    }

    [TestMethod]
    public async Task SerializeObjectAsync_SelfReferencingList_ThrowsException()
    {
        // Arrange
        List<object> input = [];
        input.Add(input);

        var memoryStream = new MemoryStream();

        // Assert
        await Assert.ThrowsExceptionAsync<BencodeOutOfRangeException>(
            async () => await BencodeWriter.SerializeObjectAsync(memoryStream, input));
    }

    [TestMethod]
    [ExpectedException(typeof(InvalidDataException))]
    public async Task SerializeObjectAsync_InvalidType_ThrowsInvalidDataException()
    {
        // Arrange
        var input = new { invalid = "object" }; // anonymous type not supported by BencodeWriter
        var memoryStream = new MemoryStream();

        // Act
        await BencodeWriter.SerializeObjectAsync(memoryStream, input);
    }

    // Additional test case to handle cancellation
    [TestMethod]
    public async Task SerializeObjectAsync_WithCancellation_ThrowsOperationCanceledException()
    {
        // Arrange
        const string input = "string value";
        var memoryStream = new MemoryStream();
        
        var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync(); // Cancel the token before use

        // Act & Assert
        await Assert.ThrowsExceptionAsync<OperationCanceledException>(
            async () => await BencodeWriter.SerializeObjectAsync(memoryStream, input, null, cancellationTokenSource.Token));
    }
}