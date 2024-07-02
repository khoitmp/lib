namespace UserContext.Lib.Extension;

public static class TokenExtension
{
    private static readonly bool _enableEncryption = false;
    private static readonly string _seperator = "_";

    public static string GenerateToken(params object[] inputs)
    {
        var value = string.Join(_seperator, inputs);
        if (_enableEncryption)
            return value.Encrypt();
        return value;
    }

    public static (bool Valid, (Guid Id, string UserName, string Role) Claims) ValidateToken(this string token)
    {
        try
        {
            if (string.IsNullOrEmpty(token))
                return (false, (Guid.Empty, null, null));

            var value = token;

            if (_enableEncryption)
                value = token.Decrypt();

            var claims = value.Split(_seperator);
            var id = claims[0].ConvertTo<Guid>();
            var userName = claims[1];
            var role = claims[2];
            var valid = id != Guid.Empty
                        && !string.IsNullOrEmpty(userName)
                        && !string.IsNullOrEmpty(role);

            return (valid, (id, userName, role));
        }
        catch
        {
            return (false, (Guid.Empty, null, null));
        }
    }
}