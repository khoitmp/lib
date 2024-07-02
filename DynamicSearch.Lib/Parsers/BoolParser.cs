namespace DynamicSearch.Lib.Service;

internal class BoolParser : IValueParser<bool>
{
    public bool Parse(string value)
    {
        return bool.Parse(value);
    }
}