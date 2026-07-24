using DsgOmnichannel.Worker;
using DsgOmnichannel.Worker.Startup;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWorkerInfrastructure(builder.Configuration);
builder.Services.AddWorkerMessaging(builder.Configuration);
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
