namespace ForkForward.Mapper;

/// <summary>
/// Defines a naming convention strategy
/// </summary>
public interface INamingConvention
{
    string[] Split(string input);
    string SeparatorCharacter { get; }
}
public sealed class ExactMatchNamingConvention : INamingConvention
{
    public static readonly ExactMatchNamingConvention Instance = new();
    public string[] Split(string _) => [];
    public string SeparatorCharacter => null;
}
public sealed class PascalCaseNamingConvention : INamingConvention
{
    public static readonly PascalCaseNamingConvention Instance = new();
    public string SeparatorCharacter => "";
    public string[] Split(string input)
    {
        List<string> result = null;
        int lower = 0;
        for(int index = 1; index < input.Length; index++)
        {
            if (char.IsUpper(input[index]))
            {
                result ??= [];
                result.Add(input[lower..index]);
                lower = index;
            }
        }
        if (result == null)
        {
            return [];
        }
        result.Add(input[lower..]);
        return [..result];
    }
    // The other way to read a name that has a run of capitals in it: the run is one word, and its last capital starts
    // the next word when a lower case letter follows it. FormXML is Form, XML and XMLFile is XML, File, where Split
    // says Form, X, M, L and X, M, L, File. A digit is neither: it stays in the word it follows, as it does in Split,
    // so HTML5Parser is HTML5, Parser. Null when the name has no run of capitals, that is, when Split says the same,
    // and when the name is a single word read this way.
    internal static string[] SplitRunsOfCapitals(string input)
    {
        int words = 1;
        bool hasRuns = false;
        for (int index = 1; index < input.Length; index++)
        {
            if (!char.IsUpper(input[index]))
            {
                continue;
            }
            if (StartsWord(input, index))
            {
                words++;
            }
            else
            {
                hasRuns = true;
            }
        }
        if (!hasRuns || words == 1)
        {
            return null;
        }
        var result = new string[words];
        int lower = 0;
        words = 0;
        for (int index = 1; index < input.Length; index++)
        {
            if (char.IsUpper(input[index]) && StartsWord(input, index))
            {
                result[words++] = input[lower..index];
                lower = index;
            }
        }
        result[words] = input[lower..];
        return result;
        static bool StartsWord(string input, int index) => !char.IsUpper(input[index - 1]) || (index + 1 < input.Length && char.IsLower(input[index + 1]));
    }
}
public sealed class LowerUnderscoreNamingConvention : INamingConvention
{
    public static readonly LowerUnderscoreNamingConvention Instance = new();
    public string SeparatorCharacter => "_";
    public string[] Split(string input) => input.Split('_', StringSplitOptions.RemoveEmptyEntries);
}