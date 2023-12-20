using Aspire.V1;
using Grpc.Core;
using k8s;
using k8s.Models;
using Newtonsoft.Json;
namespace AspireGrpcService.Services
{

    public class AspireService : Aspire.V1.DashboardService.DashboardServiceBase
    {
        private readonly ILogger _logger;
        private readonly KubernetesClientConfiguration _kubernetesConfig;
        private readonly Kubernetes _kubernetesClient;
        private IServerStreamWriter<WatchResourcesUpdate>? _currentWatchResourcesUpdateStream;

        public AspireService(ILogger<AspireService> logger) 
        {
            _logger = logger;
            _kubernetesConfig = KubernetesClientConfiguration.InClusterConfig();
            _kubernetesClient = new Kubernetes(_kubernetesConfig);
        }

        public override Task<ApplicationInformationResponse> GetApplicationInformation(ApplicationInformationRequest request, ServerCallContext context)
        {
            // TODO - Confirm if we can use "tags" to identity Aspire Apps in ACA.
            return base.GetApplicationInformation(request, context);
        }

        public override async Task WatchResources(WatchResourcesRequest request, IServerStreamWriter<WatchResourcesUpdate> responseStream, ServerCallContext context)
        {
            _currentWatchResourcesUpdateStream = responseStream;

            // Creates the watcher
            var podsWatchResponse = _kubernetesClient.CoreV1.ListNamespacedPodWithHttpMessagesAsync("k8se-apps", watch: true);
            var podWatcher = podsWatchResponse.Watch<V1Pod, V1PodList>(async (type, item) =>
            {
                Console.WriteLine($"Pod event of type {type} detected for {item.Metadata.Name}");
                var reply = new WatchResourcesUpdate()
                {
                    InitialData = new InitialResourceData()
                    {
                        ResourceTypes = { new ResourceType() { DisplayName = $"{type} pod: {item.Metadata.Name}" } }
                    }
                };
                await _currentWatchResourcesUpdateStream.WriteAsync(reply);
            });

            // Gets the initial data and return it
            var podsList = await _kubernetesClient.CoreV1.ListNamespacedPodAsync("k8se-apps");
            var initialReply = new WatchResourcesUpdate()
            {
                InitialData = new InitialResourceData()
                {
                    ResourceTypes = { new ResourceType() { DisplayName = $"Initial call. Found {podsList.Items.Count} pods" } }
                }
            };
            await _currentWatchResourcesUpdateStream.WriteAsync(initialReply);

            // Force wait to keep the connection open. TODO: Update
            await new Task(() => { });
        }

        public override async Task WatchResourceConsoleLogs(WatchResourceConsoleLogsRequest request, IServerStreamWriter<WatchResourceConsoleLogsUpdate> responseStream, ServerCallContext context)
        {
           var podName = request.ResourceId.Uid.ToString();
           while(!context.CancellationToken.IsCancellationRequested)
            {
                using k8s.Autorest.HttpOperationResponse<Stream> stream = await _kubernetesClient.CoreV1.ReadNamespacedPodLogWithHttpMessagesAsync(podName, _kubernetesConfig.Namespace);
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
