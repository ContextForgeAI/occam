using OccamMcp.Core.Composition;
using Microsoft.Extensions.DependencyInjection;

namespace OccamMcp.Core.Batch;

public static class BatchServiceCollectionExtensions
{
    public static IServiceCollection AddOccamBatch(this IServiceCollection services)
    {
        services.AddOccamCore();
        services.AddSingleton<IBatchJobStore, JsonFileBatchJobStore>();
        services.AddSingleton<IBatchJobService, BatchJobService>();
        services.AddHostedService<BatchJobProcessor>();
        return services;
    }
}
