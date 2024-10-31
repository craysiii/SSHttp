namespace SSHttp.Data.Responses;

public class ErrorsResponse
{
    public string[] Errors { get; set; }

    public ErrorsResponse(string error)
    {
        Errors = [error];
    }

    public ErrorsResponse(string[] errors)
    {
        Errors = errors;
    }

    public ErrorsResponse(string[][] errors)
    {
        Errors = errors.SelectMany(x => x).ToArray();
    }

    public ErrorsResponse(IDictionary<string, string[]> errors)
    {
        Errors = errors.Values.SelectMany(x => x).ToArray();
    }
}