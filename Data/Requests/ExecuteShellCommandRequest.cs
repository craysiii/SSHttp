namespace SSHttp.Data.Requests;

public class ExecuteShellCommandRequest
{
    [Required]
    public string Command { get; set; } = string.Empty;
    public int Timeout { get; set; }
}