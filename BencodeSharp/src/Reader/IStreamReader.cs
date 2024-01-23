namespace BencodeSharp.Reader;

internal interface IStreamReader
{
    int PeekChar();
    char ReadChar();
    bool TryPeek(out char? c);
    byte[] ReadBytesUnsafe(int numBytesToRead);
    public long GetPosition();
}