using System;
using System.Net;
using System.Threading.Tasks;
using AutoNumber.Documents;
using AutoNumber.Interfaces;
using AutoNumber.Options;
using Microsoft.Azure.Cosmos;

namespace AutoNumber.DataStores;

internal class CosmosDbOptimisticDataStore : IOptimisticDataStore
{
    private readonly CosmosClient _cosmosClient;
    private readonly AutoNumberOptions _options;
    private readonly Container _container;

    public CosmosDbOptimisticDataStore(CosmosClient cosmosClient, AutoNumberOptions options)
    {
        _cosmosClient = cosmosClient ?? throw new ArgumentNullException(nameof(cosmosClient));
        _options = options ?? throw new ArgumentNullException(nameof(options)); ;
            
        _container = cosmosClient.GetContainer(options.DatabaseId, options.ContainerName);
    }

    public AutoNumberState GetAutoNumberState(string scopeName)
    {
        try
        {
            var autoNumberState =
                _container.ReadItemAsync<AutoNumberState>(scopeName, new PartitionKey(scopeName)).GetAwaiter().GetResult();

            return autoNumberState.Resource;
        }
        catch (CosmosException cosmosException)
        {
            if (cosmosException.StatusCode is HttpStatusCode.NotFound)
                return InitializeAutoNumberStateForScope(scopeName);

            throw;
        }
    }

    public async Task<AutoNumberState> GetAutoNumberStateAsync(string scopeName)
    {
        try
        {
            var autoNumberState =
                await _container.ReadItemAsync<AutoNumberState>(scopeName, new PartitionKey(scopeName)).ConfigureAwait(false);

            return autoNumberState.Resource;
        }
        catch (CosmosException cosmosException)
        {
            if (cosmosException.StatusCode is HttpStatusCode.NotFound)
                return await InitializeAutoNumberStateForScopeAsync(scopeName);

            throw;
        }
    }

    public async Task<bool> InitializeAsync()
    {
        var database = _cosmosClient.GetDatabase(_options.DatabaseId);
        var containerProperties = new ContainerProperties
        {
            Id = _options.ContainerName,
            PartitionKeyPath = "/pk"
        };
        var newContainer = await database.CreateContainerIfNotExistsAsync(containerProperties).ConfigureAwait(false);

        return newContainer is {StatusCode: HttpStatusCode.Created or HttpStatusCode.OK};
    }

    public bool Initialize()
    {
        var database = _cosmosClient.GetDatabase(_options.DatabaseId);
        var containerProperties = new ContainerProperties
        {
            Id = _options.ContainerName,
            PartitionKeyPath = "/pk"
        };
        var newContainer = database.CreateContainerIfNotExistsAsync(containerProperties).GetAwaiter().GetResult();

        return newContainer is { StatusCode: HttpStatusCode.Created or HttpStatusCode.OK };
    }

    public bool TryOptimisticWrite(AutoNumberState autoNumberState)
    {
        autoNumberState = autoNumberState ?? throw new ArgumentNullException(nameof(autoNumberState));

        try
        {
            var itemRequestOptions = new ItemRequestOptions
            {
                IfMatchEtag = autoNumberState.ETag
            };
            
            _container.UpsertItemAsync(autoNumberState, new PartitionKey(autoNumberState.Pk), itemRequestOptions).GetAwaiter().GetResult();
        }
        catch (CosmosException cosmosException)
        {
            if (cosmosException.StatusCode == HttpStatusCode.PreconditionFailed)
                return false;

            throw;
        }

        return true;
    }

    public async Task<bool> TryOptimisticWriteAsync(AutoNumberState autoNumberState)
    {
        autoNumberState = autoNumberState ?? throw new ArgumentNullException(nameof(autoNumberState));

        try
        {
            var itemRequestOptions = new ItemRequestOptions
            {
                IfMatchEtag = autoNumberState.ETag
            };

            await _container.UpsertItemAsync(autoNumberState, new PartitionKey(autoNumberState.Pk), itemRequestOptions).ConfigureAwait(false);
        }
        catch (CosmosException cosmosException)
        {
            if (cosmosException.StatusCode == HttpStatusCode.PreconditionFailed)
                return false;

            throw;
        }

        return true;
    }

    private async Task<AutoNumberState> InitializeAutoNumberStateForScopeAsync(string scopeName)
    {
        if (!_options.InitialScopesAvailableNumber.TryGetValue(scopeName, out var initialAvailableNumber))
        {
            initialAvailableNumber = 1;
        }

        var autoNumberState = new AutoNumberState
        {
            Id = scopeName,
            NextAvailableNumber = initialAvailableNumber
        };

        try
        {
            var result = await _container.CreateItemAsync(autoNumberState, new PartitionKey(autoNumberState.Pk)).ConfigureAwait(false);

            return result.Resource;
        }
        catch (CosmosException cosmosException)
        {
            if (cosmosException.StatusCode != HttpStatusCode.Conflict)
                throw;
        }

        return autoNumberState;
    }

    private AutoNumberState InitializeAutoNumberStateForScope(string scopeName)
    {
        if (!_options.InitialScopesAvailableNumber.TryGetValue(scopeName, out var initialAvailableNumber))
        {
            initialAvailableNumber = 1;
        }

        var autoNumberState = new AutoNumberState
        {
            Id = scopeName,
            NextAvailableNumber = initialAvailableNumber
        };

        try
        {
            var result =
                _container.CreateItemAsync(autoNumberState, new PartitionKey(autoNumberState.Pk)).GetAwaiter().GetResult();

            return result.Resource;
        }
        catch (CosmosException cosmosException)
        {
            if (cosmosException.StatusCode != HttpStatusCode.Conflict)
                throw;
        }

        return autoNumberState;
    }
}