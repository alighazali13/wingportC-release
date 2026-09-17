namespace GamePort.Cashier.Common;

public static class PersianFormat
{
    private static readonly char[] PersianDigits =
    {
        '\u06F0', '\u06F1', '\u06F2', '\u06F3', '\u06F4',
        '\u06F5', '\u06F6', '\u06F7', '\u06F8', '\u06F9'
    };

    public static string Digits(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        var chars = input.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (chars[i] is >= '0' and <= '9')
            {
                chars[i] = PersianDigits[chars[i] - '0'];
            }
        }

        return new string(chars);
    }

    public static string Number(int value) => Digits(value.ToString("N0"));

    public static string Number(decimal value) => Digits(value.ToString("N0"));
}
