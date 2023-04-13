using System;
using System.Reflection;
using System.Text.Json.Serialization;
using GreenPipes;
using MassTransit;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using Play.Common.Configuration;
using Play.Common.Identity;
using Play.Common.MassTransit;
using Play.Common.MongoDB;
using Play.Common.Settings;
using Play.Identity.Contracts;
using Play.Inventory.Contracts;
using Play.Trading.Service.Entities;
using Play.Trading.Service.Exceptions;
using Play.Trading.Service.Settings;
using Play.Trading.Service.StateMachines;

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
            .AddAuthorization();

        AddMassTransit(services);


        services
            .AddControllers(opt =>
            {
                opt.SuppressAsyncSuffixInActionNames = false;
            })
            .AddJsonOptions(o =>
            {
                o.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
            });

        services
            .AddSwaggerGen(c => { c.SwaggerDoc("v1", new OpenApiInfo { Title = "Play.Trading.Service", Version = "v1" }); });
    }

    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
            app.UseSwagger();
            app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Play.Trading.Service v1"));
        }

        app.UseHttpsRedirection();

        app.UseRouting();

        app.UseAuthorization();

        app.UseAuthentication();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
        });
    }

    private void AddMassTransit(IServiceCollection services)
    {
        var serviceSettings = Configuration.GetSection<ServiceSettings>();
        var mongoDbSettings = Configuration.GetSection<MongoDbSettings>();
        var queueSettings = Configuration.GetSection<QueueSettings>();

        services.AddMassTransit(massTransitCfg =>
        {
            massTransitCfg.UsingPlayEconomyRabbitMQ(retryCfg =>
            {
                retryCfg.Interval(3, TimeSpan.FromSeconds(5));
                retryCfg.Ignore<UnknownItemException>();
            });

            massTransitCfg.AddConsumers(Assembly.GetEntryAssembly());

            massTransitCfg.AddSagaStateMachine<PurchaseStateMachine, PurchaseState>(cfg =>
            {
                cfg.UseInMemoryOutbox();
            }).MongoDbRepository(repositoryCfg =>
            {
                repositoryCfg.Connection = mongoDbSettings.ConnectionString;
                repositoryCfg.DatabaseName = serviceSettings.Name;
            });
        });

        EndpointConvention.Map<GrantItems>(new Uri(queueSettings.GrantItemsQueueAddress));
        EndpointConvention.Map<DebitGil>(new Uri(queueSettings.DebitGilQueueAddress));
        EndpointConvention.Map<SubstractItems>(new Uri(queueSettings.SubstractItemsQueueAddress));

        services.AddMassTransitHostedService();
        services.AddGenericRequestClient();
    }
}
