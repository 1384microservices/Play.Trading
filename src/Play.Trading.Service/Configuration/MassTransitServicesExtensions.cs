using System;
using System.Reflection;
using GreenPipes;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Play.Common.Configuration;
using Play.Common.MassTransit;
using Play.Common.Settings;
using Play.Identity.Contracts;
using Play.Inventory.Contracts;
using Play.Trading.Service.Exceptions;
using Play.Trading.Service.Settings;
using Play.Trading.Service.StateMachines;

namespace Play.Trading.Service.Configuration;

public static class MassTransitServicesExtensions
{
    public static IServiceCollection AddMassTransit(this IServiceCollection services, IConfiguration configuration)
    {
        var serviceSettings = configuration.GetSection<ServiceSettings>();
        var mongoDbSettings = configuration.GetSection<MongoDbSettings>();
        var queueSettings = configuration.GetSection<QueueSettings>();

        services.AddMassTransit(massTransitCfg =>
        {
            massTransitCfg
                .UsingPlayEconomyMessageBroker(configuration, retryCfg =>
                {
                    retryCfg.Interval(3, TimeSpan.FromSeconds(5));
                    retryCfg.Ignore<UnknownItemException>();
                });

            massTransitCfg.AddConsumers(Assembly.GetEntryAssembly());

            massTransitCfg.AddSagaStateMachine<PurchaseStateMachine, PurchaseState>(cfg =>
                {
                    cfg.UseInMemoryOutbox();
                })
                .MongoDbRepository(repositoryCfg =>
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

        return services;
    }
}
