using BencodeSharp.Exceptions;
using BencodeSharp.Reader;
using SharpFuzz;

namespace BencodeSharp.Fuzz
{
    public class Program
    {
        public static void Main()
        {
            Fuzzer.OutOfProcess.Run(stream =>
            {
                try
                {
                    _ = BencodeReader.Deserialize<SortedDictionary<string, object>>(stream);
                }
                catch (BencodeException)
                {
                }
            });
        }
    }
}