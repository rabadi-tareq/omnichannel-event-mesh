using System.Security.Claims;
using System.Text;
using DsgOmnichannel.Api.HealthChecks;
using DsgOmnichannel.Domain.Events;
using DsgOmnichannel.Infrastructure.Persistence;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

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
    x.AddEntityFrameworkOutbox<ApplicationDbContext>(options =>
    {
        options.UseSqlServer();
        options.UseBusOutbox();
    });

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

var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "DsgOmnichannel.Local";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "DsgOmnichannel.Api";
var jwtSigningKey = builder.Configuration["Jwt:SigningKey"] ?? "DsgOmnichannel.Local.Dev.Signing.Key.12345";

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSigningKey)),
            ClockSkew = TimeSpan.Zero,
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireCustomerRole", policy => policy.RequireClaim(ClaimTypes.Role, "Customer"));
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication();
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
