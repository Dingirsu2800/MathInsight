using System.Security.Cryptography;

namespace MathInsight.Modules.TestGen.Generation;

public sealed class SecureTestCodeGenerator : ITestCodeGenerator
{
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private const int CodeLength = 8;

    public string Generate()
    {
        Span<char> code = stackalloc char[CodeLength];
        for (var index = 0; index < code.Length; index++)
            code[index] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];

        return new string(code);
    }
}
