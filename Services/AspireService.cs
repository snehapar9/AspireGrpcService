using Aspire.V1;
using Azure.Core;
using Google.Protobuf.Collections;
using Grpc.Core;
using k8s;
using k8s.Autorest;
using k8s.KubeConfigModels;
using k8s.Models;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using Prometheus;
using System;
using System.ComponentModel;
using System.Diagnostics.Metrics;
using System.Reflection.PortableExecutable;
using System.Text;
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
        private const int MaxLinesPerMessage = 10;

        public AspireService(ILogger<AspireService> logger)
        {
            _logger = logger;
            _kubernetesConfig = KubernetesClientConfiguration.InClusterConfig();
            _kubernetesClient = new Kubernetes(_kubernetesConfig);
        }

        public override async Task<ApplicationInformationResponse> GetApplicationInformation(ApplicationInformationRequest request, ServerCallContext context)
        {
            var billingSecret = await _kubernetesClient.CoreV1.ReadNamespacedSecretAsync("billing", "k8se-system");
            billingSecret.Data.TryGetValue("KubeEnvironmentId", out byte[]? kubeEnvironmentId);
            var kubeEnvironmentName = kubeEnvironmentId != null ? Encoding.UTF8.GetString(kubeEnvironmentId).Split('/').Last() : null;
            var response = new ApplicationInformationResponse
            {
                ApplicationName = kubeEnvironmentName
            };
            return response;
        }

        public override async Task WatchResources(WatchResourcesRequest request, IServerStreamWriter<WatchResourcesUpdate> responseStream, ServerCallContext context)
        {
            _currentWatchResourcesUpdateStream = responseStream;

            //Get initial data and write to response stream
            await WriteInitialDataToStream(responseStream);

            // Watch and write to stream
            var disposableWatcher = await WatchAndWriteChangesToStreamAsync(responseStream, context.CancellationToken);

            // Wait until the cancellation is requested
            while (!context.CancellationToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromMinutes(1), context.CancellationToken).ContinueWith(task => { });
            }
            Console.WriteLine($"WatchResources connection canceled");
            // Make sure we dispose the pod watcher.
            disposableWatcher.Dispose();
            Console.WriteLine($"WatchResources connection cleaned up");
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

                var result = labels.TryGetValue("containerapps.io/app-name", out string containerAppName);
                if (!result)
                {
                    _logger.LogDebug($"The Pod {pod.Metadata.Name} does not have an entry for the key app. Skipping to next pod.");
                    continue;
                }

                watchResourcesUpdate.InitialData.Resources.Add(await ConvertContainerAppPodToResourceAsync(pod, containerAppName));
                watchResourcesUpdate.InitialData.ResourceTypes.Add(new ResourceType() { UniqueName = pod.Metadata.Name, DisplayName = pod.Metadata.Name });
            }

            await responseStream.WriteAsync(watchResourcesUpdate);
        }

        private async Task<IDisposable> WatchAndWriteChangesToStreamAsync(IServerStreamWriter<WatchResourcesUpdate> responseStream, CancellationToken cancellationToken)
        {
            // Creates the watcher
            var podsWatchResponse = await _kubernetesClient.CoreV1.ListNamespacedPodWithHttpMessagesAsync(_kubernetesConfig.Namespace, watch: true);

            var podWatcher = podsWatchResponse.Watch<V1Pod, V1PodList>(async (type, pod) =>
            {
                Console.WriteLine("WATCH POD ON EVENT: " + type + " - " + pod.Metadata.Name);

                if (cancellationToken.IsCancellationRequested)
                {
                    Console.WriteLine("Cancellation already requested for this stream. Not processing the event.");
                    return;
                }

                var labels = pod.Labels();
                if (labels.IsNullOrEmpty())
                {
                    Console.WriteLine($"Unable to fetch app name because Pod: {pod.Metadata.Name} does not contain labels. Skipping to next pod.");
                    return;
                }

                var result = labels.TryGetValue("containerapps.io/app-name", out string containerAppName);
                if (!result)
                {
                    Console.WriteLine($"The Pod {pod.Metadata.Name} does not have an entry for the key app. Skipping to next pod.");
                    return;
                }

                Console.WriteLine($"Pod event of type {type} detected for {pod.Metadata.Name}");
                if (type.Equals(WatchEventType.Added) || type.Equals(WatchEventType.Modified))
                {
                    var watchResourcesUpdate = new WatchResourcesUpdate()
                    {
                        Changes = new WatchResourcesChanges()
                        {
                            Value =
                            {
                                new WatchResourcesChange()
                                {
                                    Upsert = await ConvertContainerAppPodToResourceAsync(pod, containerAppName)
                                }
                            }
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
                            Value = {
                                new WatchResourcesChange()
                                {
                                    Delete = new ResourceDeletion
                                    {
                                        ResourceName = pod.Metadata.Name,
                                        ResourceType = "Pod"
                                    }
                                }
                            }
                        }
                    };
                    await responseStream.WriteAsync(watchResourcesUpdate);

                }
            });
            return podWatcher;
        }

        private async Task<Resource> ConvertContainerAppPodToResourceAsync(V1Pod pod, string containerAppName)
        {
            var appPodsList = await _kubernetesClient.CoreV1.ListNamespacedPodAsync(_kubernetesConfig.Namespace, labelSelector: $"containerapps.io/app-name={containerAppName}");
            var isTerminating = appPodsList.Items.Any(pod => pod.Metadata.DeletionTimestamp.HasValue);
            // There must be at least one Container App container in a Container Apps
            var container = pod.Spec.Containers.FirstOrDefault(container => container.Env.Any(envVar => envVar.Name == "CONTAINER_APP_NAME" && envVar.Value == containerAppName));
            if (container == null)
            {
                throw new Exception($"Container app {containerAppName} has no matching container");
            }

            var resource = new Resource()
            {
                DisplayName = containerAppName,
                Name = containerAppName,
                CreatedAt = pod.Status.StartTime.HasValue ? Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(pod.Status.StartTime.Value) : Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(DateTime.UtcNow), // Use fake value until optional
                Uid = pod.Uid(),
                ResourceType = "Container",
                State = isTerminating ? "Restarting" : pod.Status.Phase == "Succeeded" ? "Pending" : pod.Status.Phase,//.Count.ToString(),// item.Status.Phase,
                Properties =
                {
                    new ResourceProperty()
                    {
                        Name = pod.Metadata.Name,
                        DisplayName = "Pod"
                    }
                }
            };

            var endpoints = new List<Aspire.V1.Endpoint>();
            var endpointUrl = container.Env.FirstOrDefault(envVar => envVar.Name == "CONTAINER_APP_HOSTNAME")?.Value;
            if (endpointUrl != null)
            {
                endpoints.Add(new Aspire.V1.Endpoint() { EndpointUrl = $"https://{endpointUrl}" });
            }
            resource.Endpoints.Add(endpoints);

            var environmentVariables = container.Env.Select(envVar => new EnvironmentVariable() { Name = envVar.Name, Value = envVar.Value ?? (envVar.ValueFrom?.ConfigMapKeyRef != null ? "<FROM CONFIGMAP>" : "<OTHER>") });
            if (environmentVariables != null)
            {
                resource.Environment.Add(environmentVariables);
            }

            return resource;
        }

        public override async Task WatchResourceConsoleLogs(WatchResourceConsoleLogsRequest request, IServerStreamWriter<WatchResourceConsoleLogsUpdate> responseStream, ServerCallContext context)
        {
            var label = $"app=grpc-service";
            _logger.LogInformation($"Label : {label}");
            var pods = await _kubernetesClient.CoreV1.ListNamespacedPodAsync(_kubernetesConfig.Namespace, labelSelector: label);

            // Aspire application only deployed to single pod using AZD
            if (pods.Items.Count > 1)
            {
                _logger.LogWarning($"Expected only one pod to match label {label} but found {pods.Items.Count} pods.");
            }

            var pod = pods.Items[0];
            //var container = pod.Spec.Containers.FirstOrDefault(container => container.Env.Any(envVar => envVar.Name == "CONTAINER_APP_NAME" && envVar.Value == request.ResourceName));

            //// Container's name is the app's name (Verified by deploying an aspire app from AZD).
            //if (container == null)
            //{
            //    _logger.LogError($"Container with name {request.ResourceName} is not found.");
            //    return;
            //}

            // Stream recent logs and keep connection open until cancellation token is requested by setting `follow` to true
            using (var stream = await _kubernetesClient.CoreV1.ReadNamespacedPodLogAsync(pod.Metadata.Name, _kubernetesConfig.Namespace, container: "grpc-service-container", follow: true, tailLines: 1000, cancellationToken: context.CancellationToken))
            {
                using (var reader = new StreamReader(stream))
                {
                    int count = 0;
                    // Register a call to Close in the cancellation token's Register method
                    context.CancellationToken.Register(() =>
                    {
                        _logger.LogWarning($"Cancellation token triggered. Closing StreamReader.");
                        reader.Close();
                        stream.Close();
                    });

                    try
                    {
                        while (!reader.EndOfStream)
                        {
                            var logsUpdate = new WatchResourceConsoleLogsUpdate();
                            int linesSent = 0;

                            // While not end of stream and lines available
                            while (linesSent <= MaxLinesPerMessage)
                            {
                                var logLine = await reader.ReadLineAsync(context.CancellationToken);
                                logsUpdate.LogLines.Add(new ConsoleLogLine { Text = logLine });
                                linesSent++;
                            }

                            // Asynchronously write the log update to the response stream
                            await responseStream.WriteAsync(logsUpdate, context.CancellationToken);
                            logsUpdate.LogLines.Clear();

                            // LogWarning only 20 times
                            for( int i = 0; i<1500; i++)
                            {
                                _logger.LogWarning($"Test - Add logs to pod {i}");
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.LogWarning("Operation was cancelled.");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Error: {ex.Message}");
                        // Handle other exceptions as needed
                    }
                }
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
