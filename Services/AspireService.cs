using Aspire.V1;
using Azure.Core;
using Grpc.Core;
using k8s;
using k8s.Models;
using Microsoft.IdentityModel.Tokens;
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
            await WriteInitialDataToStream(responseStream);

            // Watch and write to stream
            await WatchAndWriteChangesToStream(responseStream);

            // Wait until the cancellation is requested
            while (!context.CancellationToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromMinutes(1), context.CancellationToken).ContinueWith(task => { });
            }
            Console.WriteLine($"WatchResources connection canceled");
        }

        private async Task WriteInitialDataToStream(IServerStreamWriter<WatchResourcesUpdate> responseStream)
        {
            // Gets the initial data and return it
            var podsList = await _kubernetesClient.CoreV1.ListNamespacedPodAsync(_kubernetesConfig.Namespace);
            var watchResourcesUpdate = new WatchResourcesUpdate() { InitialData = new InitialResourceData() };
            var initialData = new InitialResourceData();
  
            foreach (var pod in podsList)
            {
                var labels = pod.Labels();
                if (labels.IsNullOrEmpty())
                {
                    _logger.LogDebug($"Unable to fetch app name because Pod: {pod.Metadata.Name} does not contain labels.Skipping to next pod.");
                    continue;
                }

                var result = labels.TryGetValue("containerapps.io/app-name", out string appName);
                if (!result)
                {
                    _logger.LogDebug($"The Pod {pod.Metadata.Name} does not have an entry for the key app. Skipping to next pod.");
                    continue;
                }

                _logger.LogDebug($"App name: {appName}");
                _logger.LogDebug($"Result: {result}");
                initialData.Resources.Add(new Resource() { Name = appName, Commands = { new ResourceCommandRequest() { CommandType = "Restart" } } });
              
            }

            watchResourcesUpdate.InitialData = initialData;
            await responseStream.WriteAsync(watchResourcesUpdate);
        }

        private async Task WatchAndWriteChangesToStream(IServerStreamWriter<WatchResourcesUpdate> responseStream)
        {
            // Creates the watcher
            var podsWatchResponse = await _kubernetesClient.CoreV1.ListNamespacedPodWithHttpMessagesAsync(_kubernetesConfig.Namespace, watch: true);
            var podWatcher = podsWatchResponse.Watch<V1Pod, V1PodList>(async (type, item) =>
            {
                var labels = item.Labels();
                if (labels.IsNullOrEmpty())
                {
                    _logger.LogDebug($"Unable to fetch app name because Pod: {item.Metadata.Name} does not contain labels.Skipping to next pod.");
                    return;        
                }

                var result = labels.TryGetValue("containerapps.io/app-name", out string appName);
                if (!result)
                {
                    _logger.LogDebug($"The Pod {item.Metadata.Name} does not have an entry for the key app. Skipping to next pod.");
                    return;
                }

                Console.WriteLine($"Pod event of type {type} detected for {item.Metadata.Name}");
                if (type.Equals(WatchEventType.Added) || type.Equals(WatchEventType.Modified))
                {
                    var watchResourcesUpdate = new WatchResourcesUpdate()
                    {
                        Changes = new WatchResourcesChanges()
                        {
                            Value = { new WatchResourcesChange() { Upsert = new Resource { Name = item.Metadata.Name } } }
                        }
                    };
                    await responseStream.WriteAsync(watchResourcesUpdate);
                }
                else if (type.Equals(WatchEventType.Deleted))
                {
                    var watchResourcesUpdate = new WatchResourcesUpdate()
                    {
                        Changes = new WatchResourcesChanges()
                        {
                            Value = { new WatchResourcesChange() { Delete = new ResourceDeletion { ResourceName = item.Metadata.Name } } }
                        }
                    };
                    await responseStream.WriteAsync(watchResourcesUpdate);
                }
            });
        }

        public override async Task WatchResourceConsoleLogs(WatchResourceConsoleLogsRequest request, IServerStreamWriter<WatchResourceConsoleLogsUpdate> responseStream, ServerCallContext context)
        {
            var label = $"containerapps.io/app-name={request.ResourceName}";
            _logger.LogInformation($"Label : {label}");
            var pods = await _kubernetesClient.CoreV1.ListNamespacedPodAsync(_kubernetesConfig.Namespace, labelSelector: label);

            // We only have one pod deployed for each Aspire App
            if (pods.Items.Count() > 1)
            {
                _logger.LogDebug($"Multiple pods were found for the app {request.ResourceName}");
            }

            var podName = pods.Items[0].Metadata.Name;

            _logger.LogInformation($"PodName: {podName}");
            while (!context.CancellationToken.IsCancellationRequested)
            {
                var stream = await _kubernetesClient.CoreV1.ReadNamespacedPodLogAsync(podName, _kubernetesConfig.Namespace, container: request.ResourceName);
                var logsUpdate = new WatchResourceConsoleLogsUpdate();
                using var reader = new StreamReader(stream);
                while (!reader.EndOfStream)
                {
                    var logEntry = await reader.ReadLineAsync();
                    logsUpdate.LogLines.Add(new ConsoleLogLine { Text = logEntry });
                }
                await responseStream.WriteAsync(logsUpdate);

                await Task.Delay(1000);
            }
        }

        public override Task<ResourceCommandResponse> ExecuteResourceCommand(ResourceCommandRequest request, ServerCallContext context)
        {
            return base.ExecuteResourceCommand(request, context);
        }

    }
}
