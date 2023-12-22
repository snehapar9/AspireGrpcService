using Aspire.V1;
using Grpc.Core;
using k8s;
using k8s.Models;
using Newtonsoft.Json;
using static Prometheus.Exemplar;
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

        public override async Task<ApplicationInformationResponse> GetApplicationInformation(ApplicationInformationRequest request, ServerCallContext context)
        {
            // TODO - Confirm if we can use "tags" to identity Aspire Apps in ACA.
           return await base.GetApplicationInformation(request, context).ConfigureAwait(false);
            
        }

        public override async Task WatchResources(WatchResourcesRequest request, IServerStreamWriter<WatchResourcesUpdate> responseStream, ServerCallContext context)
        {
            _currentWatchResourcesUpdateStream = responseStream;

            //Get initial data and write to response stream
            var initialData = await GetInitialData();
            await responseStream.WriteAsync(initialData).ConfigureAwait(false);

            // Watch and write to stream
            await WatchAndWriteChangesToStream(responseStream);

      
            // Wait until the cancellation is requested
            while (!context.CancellationToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromMinutes(1), context.CancellationToken).ContinueWith(task => { });
            }
            Console.WriteLine($"WatchResources connection canceled");
        }

        private async Task<WatchResourcesUpdate> GetInitialData()
        {
            // Gets the initial data and return it
            var podsList = await _kubernetesClient.CoreV1.ListNamespacedPodAsync(_kubernetesConfig.Namespace);
            var pod = podsList.Items[0];
            var initialData = new WatchResourcesUpdate()
            {
                InitialData = new InitialResourceData()
                {
                    ResourceTypes = { new ResourceType() { UniqueName = "Pod" } },
                    Resources = { new Resource() { Name = "Pod", Endpoints = { new Aspire.V1.Endpoint() { EndpointUrl = pod.Status.PodIP } }, Commands = { new ResourceCommandRequest() { CommandType = "Restart" } } }

                    }
                }
            };
            return initialData;           
        }

        private async Task WatchAndWriteChangesToStream(IServerStreamWriter<WatchResourcesUpdate> responseStream)
        {
            // Creates the watcher
            var podsWatchResponse = await _kubernetesClient.CoreV1.ListNamespacedPodWithHttpMessagesAsync(_kubernetesConfig.Namespace, watch: true);
            var podWatcher = podsWatchResponse.Watch<V1Pod, V1PodList>(async (type, item) =>
            {
                
                Console.WriteLine($"Pod event of type {type} detected for {item.Metadata.Name}");
                var reply = new WatchResourcesUpdate()
                {
                    // TODO : Figure out deletion or upsert and add here.
                    Changes = new WatchResourcesChanges()
                    {
                        Value = { new WatchResourcesChange() { Delete = new ResourceDeletion() { }, Upsert = new Resource { Name = item.Metadata.Name} } }
                    }
                    
                };
                await responseStream.WriteAsync(reply);
            });
        }

        public override async Task WatchResourceConsoleLogs(WatchResourceConsoleLogsRequest request, IServerStreamWriter<WatchResourceConsoleLogsUpdate> responseStream, ServerCallContext context)
        {
           var label = $"app={request.ResourceName}";
            _logger.LogInformation($"Label : {label}");
           var pods = await _kubernetesClient.CoreV1.ListNamespacedPodAsync(_kubernetesConfig.Namespace, labelSelector: label);
           var podName = pods.Items[0].Metadata.Name;
            _logger.LogInformation($"PodName: {podName}");
           while(!context.CancellationToken.IsCancellationRequested)
            {
                var stream = await _kubernetesClient.CoreV1.ReadNamespacedPodLogAsync(podName, _kubernetesConfig.Namespace);
                var logsUpdate = new WatchResourceConsoleLogsUpdate();
                using var reader = new StreamReader(stream);
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
