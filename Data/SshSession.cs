namespace SSHttp.Data;

public class SshSession
{
    public SshClient SshClient { get; set; }
    public int Timeout { get; set; }
    public DateTime Expiry { get; set; }
    public ShellStream? ShellStream { get; set; }
}