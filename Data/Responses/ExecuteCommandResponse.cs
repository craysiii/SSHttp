namespace SSHttp.Data.Responses;

public class ExecuteCommandResponse(string[] commandResults)
{
    public string[] CommandResults { get; set; } = commandResults;
}