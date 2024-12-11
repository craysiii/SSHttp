var builder = WebApplication.CreateBuilder(args);

// Configure serialization
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
});
builder.Services.Configure<JsonOptions>(options =>
{
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
});

builder.Services.AddHttpLogging(options =>
{
    options.CombineLogs = true;
    options.LoggingFields = HttpLoggingFields.All;
});

// Configure swagger/openapi
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Version = "v1",
        Title = "SSHttp API",
        Description = "HTTP REST API to broker connections to SSH servers"
    });
});

// Register custom services with DI
builder.Services.AddSingleton<SessionBroker>();
builder.Services.AddSingleton<SimpleAuth>();

builder.WebHost.UseUrls("http://0.0.0.0:8080");

var app = builder.Build();

app.UseHttpLogging();

// Configure swagger page to be index
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.RoutePrefix = string.Empty;
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "SSHttp API");
});

// Force DI to initialize auth to generate api key if none is given
app.Services.GetService<SimpleAuth>();
app.Services.GetService<SessionBroker>();

// Create Session
app.MapPost("/session",
    (
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Default)] CreateSessionRequest sessionRequest,
        [FromServices] SessionBroker broker,
        [FromServices] SimpleAuth auth,
        HttpContext httpContext
    ) =>
    {
        if (!auth.RequestAuthorized(httpContext)) return Results.Unauthorized();
        
        var valid = MiniValidator.TryValidate(sessionRequest, out var errors);
        if (!valid) return Results.Json(new ErrorsResponse(errors), statusCode: StatusCodes.Status400BadRequest);

        var session = broker.CreateSession(sessionRequest);

        return session.Errors is null ?
            Results.Json(session.Session, statusCode: StatusCodes.Status201Created) :
            Results.Json(session.Errors, statusCode: StatusCodes.Status400BadRequest);
    })
    .Accepts<CreateSessionRequest>("application/json")
    .Produces<CreateSessionResponse>(StatusCodes.Status201Created)
    .Produces<ErrorsResponse>(StatusCodes.Status400BadRequest)
    .Produces(StatusCodes.Status401Unauthorized)
    .WithName("CreateSession")
    .WithOpenApi(operation => new OpenApiOperation(operation)
    {
        Summary = "Create Session",
        Description = "Create a new SSH session"
    });

// Send Command to Session
app.MapPost("/session/{sessionId:guid}/command",
    (
        [FromRoute] Guid sessionId,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] ExecuteCommandRequest commandRequest,
        [FromServices] SessionBroker broker,
        [FromServices] SimpleAuth auth,
        HttpContext httpContext
    ) =>
    {
        if (!auth.RequestAuthorized(httpContext)) return Results.Unauthorized();
        
        var valid = MiniValidator.TryValidate(commandRequest, out var errors);
        if (!valid) return Results.Json(new ErrorsResponse(errors), statusCode: StatusCodes.Status400BadRequest);

        var command = broker.ExecuteCommand(sessionId, commandRequest);

        return command.Errors is null ?
            Results.Json(command.Command, statusCode: StatusCodes.Status202Accepted) :
            Results.Json(command.Errors, statusCode: StatusCodes.Status400BadRequest);
    })
    .Produces<ExecuteCommandResponse>(StatusCodes.Status202Accepted)
    .Produces<ErrorsResponse>(StatusCodes.Status400BadRequest)
    .Produces(StatusCodes.Status401Unauthorized)
    .WithName("ExecuteCommand")
    .WithOpenApi(operation => new OpenApiOperation(operation)
    {
        Summary = "Execute Command",
        Description = "Execute a command against a session and receive command output as string"
    });

// Send Shell Command to Session
app.MapPost("/session/{sessionId:guid}/shellcommand",
        (
            [FromRoute] Guid sessionId,
            [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] ExecuteShellCommandRequest commandRequest,
            [FromServices] SessionBroker broker,
            [FromServices] SimpleAuth auth,
            HttpContext httpContext
        ) =>
        {
            if (!auth.RequestAuthorized(httpContext)) return Results.Unauthorized();
        
            var valid = MiniValidator.TryValidate(commandRequest, out var errors);
            if (!valid) return Results.Json(new ErrorsResponse(errors), statusCode: StatusCodes.Status400BadRequest);

            var command = broker.ExecuteShellCommand(sessionId, commandRequest);

            return command.Errors is null ?
                Results.Json(command.Command, statusCode: StatusCodes.Status202Accepted) :
                Results.Json(command.Errors, statusCode: StatusCodes.Status400BadRequest);
        })
    .Produces<ExecuteCommandResponse>(StatusCodes.Status202Accepted)
    .Produces<ErrorsResponse>(StatusCodes.Status400BadRequest)
    .Produces(StatusCodes.Status401Unauthorized)
    .WithName("ExecuteShellCommand")
    .WithOpenApi(operation => new OpenApiOperation(operation)
    {
        Summary = "Execute Shell Command",
        Description = "Execute a command against a session shell and receive command output as a string"
    });

// End Session
app.MapDelete("/session/{sessionId:guid}",
    (
        [FromRoute] Guid? sessionId,
        [FromServices] SessionBroker broker,
        [FromServices] SimpleAuth auth,
        HttpContext httpContext
    ) =>
    {
        if (!auth.RequestAuthorized(httpContext)) return Results.Unauthorized();
        
        var valid = MiniValidator.TryValidate(sessionId, out var errors);
        if (!valid) return Results.Json(new ErrorsResponse(errors), statusCode: StatusCodes.Status400BadRequest);
        
        var session = broker.RemoveSession(sessionId!.Value);
        
        return string.IsNullOrWhiteSpace(session.Error) ?
            Results.Accepted() :
            Results.Json(new ErrorsResponse(session.Error!), statusCode: StatusCodes.Status400BadRequest);
    })
    .Produces(StatusCodes.Status202Accepted)
    .Produces<ErrorsResponse>(StatusCodes.Status400BadRequest)
    .Produces(StatusCodes.Status401Unauthorized)
    .WithName("DeleteSession")
    .WithOpenApi(operation => new OpenApiOperation(operation)
    {
        Summary = "Close Session",
        Description = "Terminate SSH session"
    });

app.Run();