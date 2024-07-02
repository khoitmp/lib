namespace Kernel.Lib.Extension;

public static class JsonExtension
{
    public static T Deserialize<T>(this byte[] input)
    {
        T output = default;
        using (var mem = new MemoryStream(input))
        using (var sr = new StreamReader(mem))
        using (var jsonTextReader = new JsonTextReader(sr))
        {
            output = Defaults.JsonSerializer.Deserialize<T>(jsonTextReader);
        }
        return output;
    }

    public static byte[] Serialize(this object value)
    {
        byte[] result = default;
        using (var mem = new MemoryStream())
        using (var writer = new StreamWriter(mem))
        using (var jsonWriter = new JsonTextWriter(writer))
        {
            Defaults.JsonSerializer.Serialize(jsonWriter, value);
            jsonWriter.Flush();
            result = mem.ToArray();
        }
        return result;
    }

    public static string ToJson(this object value)
    {
        var array = Serialize(value);
        return Encoding.UTF8.GetString(array);
    }

    public static T FromJson<T>(this string jsonString)
    {
        var array = Encoding.UTF8.GetBytes(jsonString);
        return Deserialize<T>(array);
    }
}