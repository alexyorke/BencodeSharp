using System.Collections;
using System.Numerics;

namespace BencodeSharp.Tests
{
    public static class RandomObjectCreator
    {
        private static readonly Random Random = Random.Shared;

        public static object GenerateRandomObject(int maxDepth, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (maxDepth <= 0)
            {
                // Base case for recursion: return a simple type
                return GenerateSimpleRandomObject(cancellationToken);
            }

            int typeSelector = Random.Next(3); // Selects which type of object to create

            return typeSelector switch
            {
                0 => GenerateRandomList(maxDepth - 1, cancellationToken),
                1 => GenerateRandomDictionary(maxDepth - 1, cancellationToken),
                _ => GenerateSimpleRandomObject(cancellationToken),
            };
        }

        private static object GenerateSimpleRandomObject(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            int typeSelector = Random.Next(1); // Selects which simple type to create

            return typeSelector switch
            {
                0 => new BigInteger(Random.Next(int.MinValue, int.MaxValue)),
                1 => GenerateRandomByteArray(cancellationToken),
                _ => throw new InvalidOperationException("Unexpected type selector.")
            };
        }

        private static IList GenerateRandomList(int maxDepth, CancellationToken cancellationToken)
        {
            int size = Random.Next(1, 6);
            var list = new List<object>();
            for (int i = 0; i < size; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                list.Add(GenerateRandomObject(maxDepth, cancellationToken));
            }
            return list;
        }

        private static IDictionary<string, object> GenerateRandomDictionary(int maxDepth, CancellationToken cancellationToken)
        {
            int size = Random.Next(1, 6);
            var dictionary = new SortedDictionary<string, object>();
            for (int i = 0; i < size; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                dictionary.Add("key" + i, GenerateRandomObject(maxDepth, cancellationToken));
            }
            return dictionary;
        }

        private static byte[] GenerateRandomByteArray(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            int size = Random.Next(1, 6);
            byte[] byteArray = new byte[size];
            Random.NextBytes(byteArray);
            return byteArray;
        }
    }
}
