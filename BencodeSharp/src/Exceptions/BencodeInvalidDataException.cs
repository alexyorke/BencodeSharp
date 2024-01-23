namespace BencodeSharp.Exceptions;

public class BencodeInvalidDataException(string message, long? position = null)
    : BencodeException($"{message}" + (position != null ? $". Position: {position}" : string.Empty));