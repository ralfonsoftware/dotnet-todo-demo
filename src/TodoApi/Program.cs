using System.Text.Json;
using System.Text.Json.Serialization;
using Scalar.AspNetCore;
using TodoApi.Features.Todos;
using TodoApi.Storage;
using TodoApi.Telemetry;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(
        new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false)));

builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = false);

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddSingleton<ITodoStore, InMemoryTodoStore>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddTodoApiTelemetry(builder.Configuration, builder.Environment.EnvironmentName);

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

app.MapOpenApi();
app.MapScalarApiReference();
app.MapTodoEndpoints();

app.Run();

public partial class Program;
