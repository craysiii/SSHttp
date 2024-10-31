namespace SSHttp.Data.Responses;

public class CreateSessionResponse(Guid sessionId)
{
    public string SessionId { get; set; } = sessionId.ToString();
}