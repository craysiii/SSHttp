namespace SSHttp.Data.Requests;

public class CreateSessionRequest
{
    [Required]
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 22;
    public int Timeout { get; set; } = 30;
    [Required]
    public string Username { get; set; } = string.Empty;
    public string? Password { get; set; }
    public string? CertificatePath { get; set; }
    public string? CertificatePassphrase { get; set; }
}

