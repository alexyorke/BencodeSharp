namespace BencodeSharp.Writer;

public class BencodeWriterOptions
{
    private int _maxDepth = 10000;

    /// <summary>
    /// The maximum parsing depth. For example, [[[a]]] would have a parsing depth of three,
    /// because it is nested in three layers of lists. Dictionaries work similarly.
    /// This value prevents deeply nested structures from consuming all available memory,
    /// invalid Bencode structures that might be interpreted as a series of nested objects,
    /// malicious inputs, or bugs in the parser.
    /// 
    /// This value must be equal to or greater than zero.
    /// </summary>
    public int MaxDepth
    {
        get => _maxDepth;
        // ReSharper disable once UnusedMember.Global
        set => SetMaxDepth(value);
    }

    /// <summary>
    /// Sets the maximum depth for serialization, ensuring it's not less than zero.
    /// </summary>
    /// <param name="value">The value to set as the maximum depth.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is less than zero.</exception>
    private void SetMaxDepth(int value)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Max depth cannot be less than zero.");
        }

        _maxDepth = value;
    }
}