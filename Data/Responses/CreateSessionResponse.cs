namespace SSHttp.Data.Responses;

public class CreateSessionResponse()
{
    public string SessionId { get; set; } = string.Empty;
    public string Banner { get; set; } = string.Empty;
    public DateTime Expiry { get; set; }
}