using Aspire.V1;
using Azure.Core;
using Grpc.Core;
using k8s;
using k8s.Autorest;
using k8s.Models;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using System.ComponentModel;
using static Prometheus.Exemplar;
using ResourceType = Aspire.V1.ResourceType;
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
            var response = new ApplicationInformationResponse
            {
                ApplicationName = "My App"
            };
            return response;
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
            var watchResourcesUpdate = new WatchResourcesUpdate() { InitialData = new InitialResourceData() { } };

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
                // TODO - Determine public endpoint. Cannot use PodIp/HostIp because it would cancel the operation if these properties become null when pods are deleted.
                watchResourcesUpdate.InitialData.Resources.Add(new Resource() { Name = appName, DisplayName = appName, ResourceType = "Pod", CreatedAt = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(pod.CreationTimestamp().Value), Uid = pod.Uid(), State = pod.Status.Phase, Properties = { new ResourceProperty() { DisplayName = pod.Metadata.Name, Name = pod.Metadata.Name } } });
                watchResourcesUpdate.InitialData.ResourceTypes.Add(new ResourceType() { UniqueName = pod.Metadata.Name, DisplayName = pod.Metadata.Name });
            }

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
                            // Hard-coded resource type to pod because pod.Kind can be Null and would throw an exception.
                            Value = { new WatchResourcesChange() { Upsert = new Resource() { DisplayName = appName, Name = appName, CreatedAt = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(item.CreationTimestamp().Value), Uid = item.Uid(), ResourceType = "Pod", State = item.Status.Phase, Properties = { new ResourceProperty() { Name = item.Metadata.Name, DisplayName = item.Metadata.Name } } } } }
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
                            Value = { new WatchResourcesChange() { Delete = new ResourceDeletion { ResourceName = item.Metadata.Name, ResourceType = "Pod" } } }
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

            // Aspire application only deployed to single pod using AZD
            if (pods.Items.Count > 1)
            {
                _logger.LogWarning($"Expected only one pod to match label {label} but found {pods.Items.Count} pods.");
            }

            var pod = pods.Items[0];
            var container = pod.Spec.Containers.Where(c => c.Name.Equals(request.ResourceName)).FirstOrDefault();

            // Container's name is the app's name (Verified by deploying an aspire app from AZD).
            if (container == null)
            {
                _logger.LogError($"Container with name {request.ResourceName} is not found.");
                return;
            }

            while (!context.CancellationToken.IsCancellationRequested)
            {
                var stream = await _kubernetesClient.CoreV1.ReadNamespacedPodLogWithHttpMessagesAsync(pod.Metadata.Name, _kubernetesConfig.Namespace, container: container.Name);
                var logsUpdate = new WatchResourceConsoleLogsUpdate();
                var reader = new StreamReader(stream.Body);
                while (!reader.EndOfStream)
                {
                    var logEntry = await reader.ReadLineAsync();
                    logsUpdate.LogLines.Add(new ConsoleLogLine { Text = logEntry });
                }

                await responseStream.WriteAsync(logsUpdate);
                await Task.Delay(1000);
            }
        }

        public override async Task<ResourceCommandResponse> ExecuteResourceCommand(ResourceCommandRequest request, ServerCallContext context)
        {
            var response = new ResourceCommandResponse();
            if (request.CommandType.ToUpper().Equals("RESTART"))
            {
                var label = $"containerapps.io/app-name={request.Name}";
                _logger.LogInformation($"Label : {label}");
                var pods = await _kubernetesClient.CoreV1.ListNamespacedPodAsync(_kubernetesConfig.Namespace, labelSelector: label);
                if (pods.Items.Count > 1)
                {
                    _logger.LogDebug($"Multiple pods matching label : {label} were found.");
                }

                if (!pods.Items.Any())
                {
                    _logger.LogDebug($"No pod found matching label {label} for app : {request.Name}");
                    response.Kind = ResourceCommandResponseKind.Failed;
                    response.ErrorMessage = $"No pod found matching label {label} for app : {request.Name}";
                    return response;
                }

                // We only have one pod deployed for each Aspire App
                var pod = pods.Items[0];
                var podName = pod.Metadata.Name;
                var deletedPod = await _kubernetesClient.DeleteNamespacedPodAsync(podName, _kubernetesConfig.Namespace);
                TimeSpan timeoutDuration = TimeSpan.FromSeconds(60);
                DateTime startTime = DateTime.Now;
                while (DateTime.Now - startTime < timeoutDuration)
                {
                    if (deletedPod.DeletionTimestamp == null) continue;
                    response.Kind = ResourceCommandResponseKind.Succeeded;
                    return response;
                }
                response.Kind = ResourceCommandResponseKind.Failed;
                response.ErrorMessage = $"Timed out after {timeoutDuration} seconds. Pod {pod.Metadata.Name} was not deleted.";
            }
            return response;
        }

    }
}
