namespace SSHttp.Services;

public class SimpleAuth
{
    private string? ApiKey { get; set; }

    public SimpleAuth()
    {
        ApiKey = Environment.GetEnvironmentVariable("API_KEY");
        if (!string.IsNullOrWhiteSpace(ApiKey)) return;
        
        ApiKey = Guid.NewGuid().ToString("N");
        Console.WriteLine($"API_KEY is missing, generated following API_KEY: {ApiKey}");
    }

    public bool RequestAuthorized(HttpContext httpContext)
    {
        var apiKeyProvided = httpContext.Request.Headers.TryGetValue("API_KEY", out var apiKey);
        return apiKeyProvided && string.Equals(ApiKey, apiKey);
    }
}