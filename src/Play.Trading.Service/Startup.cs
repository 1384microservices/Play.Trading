using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using OpenTelemetry.Metrics;
using Play.Common.Configuration;
using Play.Common.HealthChecks;
using Play.Common.Identity;
using Play.Common.Logging;
using Play.Common.MongoDB;
using Play.Common.OpenTelemetry;
using Play.Common.Settings;
using Play.Trading.Service.Configuration;
using Play.Trading.Service.Entities;
using Play.Trading.Service.SignalR;

namespace Play.Trading.Service;

public class Startup
{
    public Startup(IConfiguration configuration)
    {
        Configuration = configuration;
    }

    public IConfiguration Configuration { get; }

    // This method gets called by the runtime. Use this method to add services to the container.
    public void ConfigureServices(IServiceCollection services)
    {
        services
            .AddMongo()
            .AddMongoRepository<CatalogItem>("CatalogItems")
            .AddMongoRepository<InventoryItem>("InventoryItems")
            .AddMongoRepository<ApplicationUser>("ApplicationUser")
            .AddJwtBearerAuthentication();

        services
            .AddMassTransit(Configuration);

        services
            .AddAuthorization();


        services
            .AddControllers(opt => opt.SuppressAsyncSuffixInActionNames = false)
            .AddJsonOptions(o => o.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull);

        services
            .AddSwaggerGen(c => c.SwaggerDoc("v1", new OpenApiInfo { Title = "Play.Trading.Service", Version = "v1" }));

        services
            .AddSingleton<IUserIdProvider, UserIdProvider>()
            .AddSingleton<MessageHub>().AddSignalR();

        services
            .AddHealthChecks()
            .AddMongoDb();

        services
            .AddSeqLogging(Configuration.GetSeqSettings())
            .AddTracing(Configuration.GetServiceSettings(), Configuration.GetSection<JaegerSettings>())
            .AddOpenTelemetryMetrics(builder =>
            {
                builder
                    .AddMeter(Configuration.GetServiceSettings().Name)
                    .AddHttpClientInstrumentation()
                    .AddAspNetCoreInstrumentation()
                    .AddPrometheusExporter()
                    ;
            });

    }

    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        if (env.IsDevelopment())
        {
            app
                .UseDeveloperExceptionPage()
                .UseSwagger()
                .UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Play.Trading.Service v1"))
                .UseCors(builder =>
                {
                    builder
                    .WithOrigins(Configuration["AllowedOrigin"])
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
                });
        }

        app
            .UseOpenTelemetryPrometheusScrapingEndpoint()
            .UseRouting()
            .UseAuthentication()
            .UseAuthorization()
            .UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapHub<MessageHub>("/messagehub");
                endpoints.MapPlayEconomyHealthChecks();
            });
    }
}
