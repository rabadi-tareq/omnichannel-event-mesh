using DsgOmnichannel.Api.Endpoints;
using DsgOmnichannel.Api.Startup;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApiCore();
builder.Services.AddApiConfiguration(builder.Configuration);
builder.Services.AddApiInfrastructure(builder.Configuration);
builder.Services.AddApiMessaging();
builder.Services.AddApiSecurity();

var app = builder.Build();

app.UseApiDeveloperExperience();
app.UseApiPipeline();
app.MapApiEndpoints();

app.Run();
