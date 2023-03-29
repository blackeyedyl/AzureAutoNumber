using System;
using System.Collections.Generic;
using AutoNumber.DataStores;
using AutoNumber.Interfaces;
using AutoNumber.Options;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AutoNumber.Extensions;

public static class ServiceCollectionExtensions
{

    public static IServiceCollection AddAutoNumber(this IServiceCollection services, IConfiguration configuration,
        Func<AutoNumberOptionsBuilder, AutoNumberOptions> builder)
    {
        if (builder == null)
            throw new ArgumentNullException(nameof(builder));

        var builderOptions = new AutoNumberOptionsBuilder(configuration);
        var options = builder(builderOptions);

        services.AddSingleton<IOptimisticDataStore, CosmosDbOptimisticDataStore>(x =>
        {
            CosmosClient cosmosClient;
            if (options.Client != null)
                cosmosClient = options.Client;
            else if (options.ConnectionString == null)
                cosmosClient = x.GetService<CosmosClient>();
            else
                cosmosClient = new CosmosClient(options.ConnectionString, new CosmosClientOptions
                {
                    ApplicationRegion = Regions.WestEurope,
                    ApplicationPreferredRegions = new List<string> { Regions.WestEurope }
                });

            return new CosmosDbOptimisticDataStore(cosmosClient, options);
        });

        services.AddSingleton<IUniqueIdGenerator, UniqueIdGenerator>(x
            => new UniqueIdGenerator(x.GetService<IOptimisticDataStore>(), options));

        return services;
    }
}