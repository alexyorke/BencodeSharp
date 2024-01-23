namespace BencodeSharp.Reader;

/// <summary>
///     Provides a smaller interface for a BinaryReader, including TryPeek
/// </summary>
internal sealed class StreamReader : IStreamReader
{
    // ReSharper disable once InconsistentNaming
    private const int EOF = -1;
    private readonly BinaryReader _baseStream;
    private long _bytesRead;

    private StreamReader(BinaryReader input)
    {
        _baseStream = input;
    }

    public int PeekChar()
    {
        return _baseStream.PeekChar();
    }

    public char ReadChar()
    {
        var readChar = _baseStream.ReadChar();
        _bytesRead += BencodeReader.DefaultEncoding.GetBytes(readChar.ToString()).Length;
        return readChar;
    }

    public bool TryPeek(out char? c)
    {
        var res = _baseStream.PeekChar();
        c = res != EOF ? (char)res : null;
        return res != EOF;
    }

    /// <summary>
    /// Read up to numBytesToRead from a stream.
    ///
    /// This method is unsafe because it might not return all the bytes
    /// that were requested to be read.
    /// </summary>
    /// <param name="numBytesToRead"></param>
    /// <returns></returns>
    public byte[] ReadBytesUnsafe(int numBytesToRead)
    {
        _bytesRead += numBytesToRead;
        return _baseStream.ReadBytes(numBytesToRead);
    }

    public long GetPosition()
    {
        return _bytesRead;
    }

    public static StreamReader Create(Stream input)
    {
        return new StreamReader(new BinaryReader(input, BencodeReader.DefaultEncoding));
    }
}