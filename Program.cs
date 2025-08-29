using Common.Library.Configuration;
using Common.Library.HealthChecks;
using Common.Library.Identity;
using Common.Library.Logging;
using Common.Library.MassTransit;
using Common.Library.MongoDB;
using Common.Library.OpenTelemetry;
using Inventory.Service.Clients;
using Inventory.Service.Entities;
using Inventory.Service.Exceptions;
using MassTransit;
using Microsoft.OpenApi.Models;
using Polly;
using Polly.Timeout;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.ConfigureAzureKeyVault(builder.Environment);

const string AllowedOriginSetting = "AllowedOrigin";

// Add services to the container.
builder.Services.AddMongo()
    .AddMongoRepository<InventoryItem>("inventoryitems")
    .AddMongoRepository<CatalogItem>("catalogitems")
    .AddMassTransitWithMessageBroker (builder.Configuration,retryConfigurator =>
    {
        retryConfigurator.Interval(3, TimeSpan.FromSeconds(5));
        retryConfigurator.Ignore(typeof(UnknownItemException));
    })
    .AddJwtBearerAuthentication();

builder.Services.AddSeqLogging(builder.Configuration)
    .AddTracing(builder.Configuration);

AddCatalogClient(builder);


builder.Services.AddControllers();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Inventory.Service", Version = "v1" });
});
builder.Services.AddHealthChecks()
    .AddMongoDbHealthCheck();




var app = builder.Build();
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseCors(_builder =>
    {
        _builder.WithOrigins(app.Configuration[AllowedOriginSetting])
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
}
app.UseOpenTelemetryPrometheusScrapingEndpoint();
// Configure the HTTP request pipeline.

app.UseHttpsRedirection();

app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

app.MapCustomHealthChecks();

app.Run();

void AddCatalogClient(WebApplicationBuilder webApplicationBuilder)
{
    Random jitterer = new Random();
    webApplicationBuilder.Services.AddHttpClient<CatalogClient>(client =>
        {
            client.BaseAddress = new Uri("https://localhost:5001");
        })
        .AddTransientHttpErrorPolicy(buil => buil.Or<TimeoutRejectedException>().WaitAndRetryAsync(
            5,
            retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))
                            + TimeSpan.FromMilliseconds(jitterer.Next(0, 1000)),
            onRetry: (outcome, timespan, retryAttempt) =>
            {
                var serviceProvider = webApplicationBuilder.Services.BuildServiceProvider();
                serviceProvider.GetService<ILogger<CatalogClient>>()?
                    .LogWarning("Delaying for {delay} seconds, then making retry {retry}", timespan.TotalSeconds,
                        retryAttempt);
            }
        ))
        .AddTransientHttpErrorPolicy(_builder => _builder.Or<TimeoutRejectedException>().CircuitBreakerAsync(
            3,
            TimeSpan.FromSeconds(15),
            onBreak: (outcome, timespan) =>
            {
                var serviceProvider = webApplicationBuilder.Services.BuildServiceProvider();
                serviceProvider.GetService<ILogger<CatalogClient>>()?
                    .LogWarning($"Opening the circuit for {timespan.TotalSeconds} seconds...");
            },
            onReset: () =>
            {
                var serviceProvider = webApplicationBuilder.Services.BuildServiceProvider();
                serviceProvider.GetService<ILogger<CatalogClient>>()?
                    .LogWarning("Closing the circuit...");
            }
        ))
        .AddPolicyHandler(Policy.TimeoutAsync<HttpResponseMessage>(1));
}
