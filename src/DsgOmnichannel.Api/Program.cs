using DsgOmnichannel.Api.HealthChecks;
using DsgOmnichannel.Domain.Events;
using DsgOmnichannel.Infrastructure.Persistence;
using MassTransit;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add API Controllers and Swagger/OpenAPI support
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 1. Register EF Core SQL Server
var databaseConnectionString = builder.Configuration.GetRequiredDatabaseConnectionString();

builder.Services.AddApiHealthChecks(builder.Configuration);

// 2. Register MassTransit with RabbitMQ
builder.Services.AddMassTransit(x =>
{
    x.UsingRabbitMq((context, cfg) =>
    {
        var rabbitHost = builder.Configuration["RabbitMQ:Host"] ?? "localhost";
        var rabbitUser = builder.Configuration["RabbitMQ:Username"] ?? "guest";
        var rabbitPass = builder.Configuration["RabbitMQ:Password"] ?? "guest";

        cfg.Host(rabbitHost, "/", h =>
        {
            h.Username(rabbitUser);
            h.Password(rabbitPass);
        });

        cfg.ConfigureEndpoints(context);
    });
});

builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(databaseConnectionString));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.MapGet("/", () => Results.Redirect("/swagger"));
app.MapApiHealthEndpoint();

app.MapPost("/test-publish", async (IPublishEndpoint publishEndpoint, string text) =>
{
    var pingEvent = new PingEvent(Guid.NewGuid(), text, DateTime.UtcNow);
    await publishEndpoint.Publish(pingEvent);

    return Results.Ok(new { Status = "Published", Message = pingEvent });
});

app.Run();
