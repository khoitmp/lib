namespace Kernel.Lib.Constant;

public static class Defaults
{
    private static JsonSerializer _js = new JsonSerializer
    {
        NullValueHandling = NullValueHandling.Ignore,
        ReferenceLoopHandling = ReferenceLoopHandling.Error,
        DateFormatString = DefaultFullDateTimeFormat,
        DateParseHandling = DateParseHandling.None,
        ContractResolver = new CamelCasePropertyNamesContractResolver()
    };

    private static JsonSerializerSettings _jss = new JsonSerializerSettings
    {
        NullValueHandling = NullValueHandling.Ignore,
        ReferenceLoopHandling = ReferenceLoopHandling.Error,
        DateFormatString = DefaultFullDateTimeFormat,
        DateParseHandling = DateParseHandling.None,
        ContractResolver = new CamelCasePropertyNamesContractResolver()
    };

    public static string DefaultFullDateTimeFormat = "yyyy-MM-ddTHH:mm:ss:ffff";
    public static string DefaultShortDateTimeFormat = "yyyy-MM-dd HH:mm:ss";
    public static string DefaultAliasDateTimeFormat = "yyyyMMddHHmmss";
    public static JsonSerializer JsonSerializer => _js;
    public static JsonSerializerSettings JsonSerializerSetting => _jss;
}