static class StringExtensions
{
    public static string Escape(this string s)
    {
        return string.Create(s.Length, s, (span, str) =>
         {
             for (int i = 0; i < str.Length; i++)
             {
                 span[i] = str[i] < 32 ? (char)(str[i] + 0x2400) : str[i];
             }
         });
    }
}