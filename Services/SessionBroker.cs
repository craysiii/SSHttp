using ConnectionInfo = Renci.SshNet.ConnectionInfo;

namespace SSHttp.Services;

public class SessionBroker
{
    private readonly ConcurrentDictionary<Guid, SshSession> _activeSessions = new();
    private readonly PeriodicTimer _sessionTimer = new(TimeSpan.FromSeconds(1));
    private readonly string _certificateDirectoryPath = Path.Join(Directory.GetDirectoryRoot(AppDomain.CurrentDomain.BaseDirectory), "certificates");

    public SessionBroker()
    {
        StartSessionTimer().ConfigureAwait(false);
    }

    private async Task StartSessionTimer()
    {
        while (await _sessionTimer.WaitForNextTickAsync())
        {
            InvalidateExpiredSessions();
        }
    }

    private void InvalidateExpiredSessions()
    {
        foreach (var (sessionId, session) in _activeSessions)
        {
            if (DateTime.UtcNow < session.Expiry) continue;
            
            session.SshClient.Disconnect();
            session.SshClient.Dispose();
            _activeSessions.TryRemove(sessionId, out _); // We shouldn't care if we fail (hopefully)
        }
    }

    public (Guid? SessionId, string? Error) CreateSession(CreateSessionRequest sessionRequest)
    {
        Guid? sessionId;
        string? error = null;

        if (string.IsNullOrWhiteSpace(sessionRequest.Password) &&
            string.IsNullOrWhiteSpace(sessionRequest.CertificatePath))
        {
            return (null, "You must provide a password or certificate path.");
        }
        
        try
        {
            sessionId = Guid.NewGuid();

            List<AuthenticationMethod> authenticationMethods = [];
            
            if (!string.IsNullOrWhiteSpace(sessionRequest.Password))
            {
                authenticationMethods.Add(
                    new PasswordAuthenticationMethod(sessionRequest.Username, sessionRequest.Password)
                );
            }

            if (!string.IsNullOrWhiteSpace(sessionRequest.CertificatePath))
            {
                var certificatePath = Path.Join(_certificateDirectoryPath, sessionRequest.CertificatePath);
                if (!File.Exists(certificatePath)) return (null, "Certificate file does not exist.");
                
                authenticationMethods.Add(
                    new PrivateKeyAuthenticationMethod(
                        sessionRequest.Username,
                        string.IsNullOrWhiteSpace(sessionRequest.CertificatePassphrase) ? 
                            new PrivateKeyFile(certificatePath) :
                            new PrivateKeyFile(certificatePath, sessionRequest.CertificatePassphrase)
                    )
                );
            }

            var connectionInfo = new ConnectionInfo(
                sessionRequest.Host,
                sessionRequest.Port,
                sessionRequest.Username,
                authenticationMethods.ToArray()
            );
            
            var client = new SshClient(connectionInfo);
            client.Connect();

            var currentTime = DateTime.UtcNow;

            var session = new SshSession
            {
                SshClient = client,
                Timeout = sessionRequest.Timeout,
                Expiry = currentTime.AddSeconds(sessionRequest.Timeout)
            };

            var success = false;

            while (!success)
            {
                success = _activeSessions.TryAdd(sessionId.Value, session);
            }
        }
        catch (Exception ex)
        {
            error = ex.Message;
            sessionId = null;
        }
        
        return (sessionId, error);
    }

    public (string[] CommandResults, string? Error) ExecuteCommand(Guid sessionId, ExecuteCommandRequest command)
    {
        string[] commandResults = [];
        string? error = null;
        
        if (!_activeSessions.TryGetValue(sessionId, out var session))
        {
            return ([], $"Session {sessionId} does not exist");
        }

        try
        {
            using var cmd = session.SshClient.RunCommand(command.Command);
            commandResults = cmd.Result.Split(command.LineDelimiter);
            session.Expiry = DateTime.UtcNow.AddSeconds(session.Timeout);
        }
        catch (Exception ex)
        {
            error = ex.Message;
        }
        
        return (commandResults, error);
    }

    public (Guid? SessionId, string? Error) RemoveSession(Guid sessionId)
    {
        if (!_activeSessions.TryGetValue(sessionId, out var session))
        {
            return (null, $"Session {sessionId} does not exist");
        }

        try
        {
            session.SshClient.Disconnect();
            session.SshClient.Dispose();
            _activeSessions.TryRemove(sessionId, out _);
        }
        catch (Exception ex)
        {
            return (null, ex.Message);
        }
        
        return (sessionId, null);
    }
}