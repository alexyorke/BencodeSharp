// ReSharper disable PropertyCanBeMadeInitOnly.Global

namespace BencodeSharp.Reader;

/// <summary>
/// Options for the Bencode reader.
/// </summary>
public class BencodeReaderOptions
{
    /// <summary>
    /// The maximum stack size. By default, it is set to 10000, which means that structures more than 10000 elements deep will
    /// throw an exception. It is recommended that this is set to a reasonable value, to prevent excessive memory usage or
    /// malicious structures.
    /// </summary>
    public int MaxStackSize { get; set; } = 10000;
    /// <summary>
    /// When parsing a Bencode string that includes elements not enclosed within any container (such as a dictionary or list)
    /// this determines whether an exception should be thrown. For example, the Bencode string "i2e3:abc" is considered unwrapped
    /// because it is not in a list or a dictionary.
    /// </summary>
    // ReSharper disable once AutoPropertyCanBeMadeGetOnly.Global
    public bool AllowUnwrappedElements { get; set; } = true;
    /// <summary>
    /// Whether to allow dictionary keys that are not byte-sorted. If this is set to false, then an exception will be thrown
    /// for dictionary keys that are not byte ordered (e.g., ordinal sorting.)
    /// </summary>
    // ReSharper disable once AutoPropertyCanBeMadeGetOnly.Global
    public bool AllowUnorderedKeys { get; set; } = false;
}