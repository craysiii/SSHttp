using ConnectionInfo = Renci.SshNet.ConnectionInfo;

namespace SSHttp.Services;

public class SessionBroker
{
    private readonly ConcurrentDictionary<Guid, SshSession> _activeSessions = new();
    private readonly PeriodicTimer _sessionTimer = new(TimeSpan.FromSeconds(1));
    private readonly string _certificateDirectoryPath = Path.Join(
        Directory.GetDirectoryRoot(AppDomain.CurrentDomain.BaseDirectory), "certificates");

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

    public (CreateSessionResponse? Session, ErrorsResponse? Errors) CreateSession(CreateSessionRequest sessionRequest)
    {
        CreateSessionResponse? sessionResponse = null;
        ErrorsResponse? errorsResponse = null;

        // Validate we're received some type of authentication or return an error
        if (string.IsNullOrWhiteSpace(sessionRequest.Password) &&
            string.IsNullOrWhiteSpace(sessionRequest.CertificatePath))
        {
            return (null, new ErrorsResponse("You must provide a password or certificate path."));
        }

        try
        {
            Guid? sessionId = Guid.NewGuid();
            List<AuthenticationMethod> authenticationMethods = [];

            // Add password authentication if applicable
            if (!string.IsNullOrWhiteSpace(sessionRequest.Password))
            {
                authenticationMethods.Add(
                    new PasswordAuthenticationMethod(sessionRequest.Username, sessionRequest.Password)
                );
            }

            // Add certificate authentication if applicable
            if (!string.IsNullOrWhiteSpace(sessionRequest.CertificatePath))
            {
                var certificatePath = Path.Join(_certificateDirectoryPath, sessionRequest.CertificatePath);
                if (!File.Exists(certificatePath))
                    return (null, new ErrorsResponse("Certificate file does not exist."));

                authenticationMethods.Add(
                    new PrivateKeyAuthenticationMethod(
                        sessionRequest.Username,
                        string.IsNullOrWhiteSpace(sessionRequest.CertificatePassphrase)
                            ? new PrivateKeyFile(certificatePath)
                            : new PrivateKeyFile(certificatePath, sessionRequest.CertificatePassphrase)
                    )
                );
            }

            // Create our ConnectionInfo object manually so we can account for either types of auth before creating client
            var connectionInfo = new ConnectionInfo(
                sessionRequest.Host,
                sessionRequest.Port,
                sessionRequest.Username,
                authenticationMethods.ToArray()
            );

            // Initialize our client and attempt to connect
            var client = new SshClient(connectionInfo);
            client.Connect();



            // Create our session object if connection was successful
            var currentTime = DateTime.UtcNow;
            var session = new SshSession
            {
                SshClient = client,
                Timeout = sessionRequest.Timeout,
                Expiry = currentTime.AddSeconds(sessionRequest.Timeout),
                ShellStream = client.CreateShellStreamNoTerminal()
            };

            // Read banner from shell stream
            var banner = new StringBuilder();
            Thread.Sleep(1000);
            while (session.ShellStream.DataAvailable)
            {
                banner.Append($"{session.ShellStream.ReadLine()}\n");
            }

            // We're not successful until we can add the session to our concurrent dictionary
            var success = false;
            while (!success)
            {
                success = _activeSessions.TryAdd(sessionId.Value, session);
            }

            // Create our response
            sessionResponse = new CreateSessionResponse
            {
                SessionId = sessionId.ToString()!,
                Banner = banner.ToString(),
                Expiry = session.Expiry
            };
        }
        catch (Exception ex)
        {
            errorsResponse = new ErrorsResponse(ex.Message);
        }
        
        return (sessionResponse, errorsResponse);
    }

    public (ExecuteCommandResponse? Command, ErrorsResponse? Errors) ExecuteCommand(Guid sessionId, ExecuteCommandRequest command)
    {
        ExecuteCommandResponse? commandResponse = null;
        ErrorsResponse? errorsResponse = null;
        var commandResults = new StringBuilder();
        
        // Return error if session cannot be found
        if (!_activeSessions.TryGetValue(sessionId, out var session))
        {
            return (null, new ErrorsResponse($"Session {sessionId} does not exist"));
        }

        try
        {
            // Execute command and create our response object
            using var cmd = session.SshClient.RunCommand(command.Command);
            commandResults.Append(cmd.Result);

            commandResponse = new ExecuteCommandResponse
            {
                CommandResults = commandResults.ToString()
            };
        }
        catch (Exception ex)
        {
            errorsResponse = new ErrorsResponse(ex.Message);
        }
        finally
        {
            // Extend session expiration by the timeout defined during session creation
            session.Expiry = DateTime.UtcNow.AddSeconds(session.Timeout);
        }
        
        return (commandResponse, errorsResponse);
    }

    public (ExecuteCommandResponse? Command, ErrorsResponse? Errors) ExecuteShellCommand(
        Guid sessionId,
        ExecuteShellCommandRequest command
    )
    {
        ExecuteCommandResponse? commandResponse = null;
        ErrorsResponse? errorsResponse = null;
        var commandResults = new StringBuilder();
        
        // Return error if session cannot be found
        if (!_activeSessions.TryGetValue(sessionId, out var session))
        {
            return (null, new ErrorsResponse($"Session {sessionId} does not exist"));
        }

        try
        {
            // Execute command and create our response object
            session.ShellStream!.WriteLine(command.Command);
            Thread.Sleep(command.Timeout * 1000);
            while (session.ShellStream.DataAvailable)
            {
                commandResults.Append($"{session.ShellStream.ReadLine()}\n");
            }

            commandResponse = new ExecuteCommandResponse
            {
                CommandResults = commandResults.ToString(),
            };
        }
        catch (Exception ex)
        {
            errorsResponse = new ErrorsResponse(ex.Message);
        }
        finally
        {
            // Extend session expiration by the timeout defined during session creation
            session.Expiry = DateTime.UtcNow.AddSeconds(session.Timeout);
        }
        
        return (commandResponse, errorsResponse);
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