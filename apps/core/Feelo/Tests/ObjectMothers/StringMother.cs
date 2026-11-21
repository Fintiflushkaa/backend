namespace Tests.ObjectMothers;

internal static class StringMother
{
    private const string Chars =
        "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    public static string Create(int length = 10)
    {
        return new string(
            Enumerable.Range(0, length)
                .Select(_ => Chars[Random.Shared.Next(Chars.Length)])
                .ToArray()
        );
    }
}
