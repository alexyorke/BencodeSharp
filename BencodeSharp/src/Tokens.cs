namespace BencodeSharp;

internal static class Tokens
{
    public record StartToken;

    public record ListStart : StartToken;

    public record DictionaryStart : StartToken;

    public record End;
}