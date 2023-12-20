using Aspire.V1;
using Grpc.Core;
using k8s;
namespace AspireGrpcService.Services
{

    public class AspireService : Aspire.V1.DashboardService.DashboardServiceBase
    {
        private readonly ILogger _logger;
        private readonly static KubernetesClientConfiguration _config = KubernetesClientConfiguration.InClusterConfig();
        private readonly static Kubernetes _client = new(_config);
        public AspireService(ILogger<AspireService> logger) 
        {
            _logger = logger;
        }

        public override Task<ApplicationInformationResponse> GetApplicationInformation(ApplicationInformationRequest request, ServerCallContext context)
        {
            // TODO - Confirm if we can use "tags" to identity Aspire Apps in ACA.
            return base.GetApplicationInformation(request, context);
        }

        public override Task WatchResources(WatchResourcesRequest request, IServerStreamWriter<WatchResourcesUpdate> responseStream, ServerCallContext context)
        {
            return base.WatchResources(request, responseStream, context);
        }

        public override async Task WatchResourceConsoleLogs(WatchResourceConsoleLogsRequest request, IServerStreamWriter<WatchResourceConsoleLogsUpdate> responseStream, ServerCallContext context)
        {
           var podName = request.ResourceId.Uid.ToString();
           while(!context.CancellationToken.IsCancellationRequested)
            {
                using k8s.Autorest.HttpOperationResponse<Stream> stream = await _client.CoreV1.ReadNamespacedPodLogWithHttpMessagesAsync(podName, _config.Namespace);
                var logsUpdate = new WatchResourceConsoleLogsUpdate();
                using var reader = new StreamReader(stream.Body);
                while (!reader.EndOfStream)
                {
                    var logEntry = await reader.ReadLineAsync();
                    logsUpdate.LogLines.Add(new ConsoleLogLine { Text = logEntry });
                }
                await responseStream.WriteAsync(logsUpdate);
                // Introduce delay for testing
                await Task.Delay(1000);
            }
        }

        public override Task<ResourceCommandResponse> ExecuteResourceCommand(ResourceCommandRequest request, ServerCallContext context)
        {
            return base.ExecuteResourceCommand(request, context);
        }

    }
}
