namespace RateLimit.Lib.Model;

public class RateLimitBucket
{
    public int Count { get; set; }
    public DateTime WindowStart { get; set; }

    public RateLimitBucket(int count, DateTime windowStart)
    {
        Count = count;
        WindowStart = windowStart;
    }
}