﻿namespace SSHttp.Data.Requests;

public class ExecuteCommandRequest
{
    [Required]
    public string Command { get; set; } = string.Empty;
}