namespace StatusImageCard.Helper
{
    public class RequireHelper
    {
        public static string Require(IConfiguration cfg, string key)
        {
            var v = cfg[key];
            if (string.IsNullOrWhiteSpace(v))
                throw new Exception($"Missing required env var: {key}");
            return v;
        }

    }
}
