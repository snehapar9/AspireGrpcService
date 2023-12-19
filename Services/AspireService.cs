using Aspire.V1;
using Grpc.Core;
using k8s;
namespace AspireGrpcService.Services
{

    public class AspireService : Aspire.V1.DashboardService.DashboardServiceBase
    {
   
        public override Task<ApplicationInformationResponse> GetApplicationInformation(ApplicationInformationRequest request, ServerCallContext context)
        {
            // Load from the default kubeconfig on the machine.
            var config = k8s.KubernetesClientConfiguration.InClusterConfig();

            var client = new Kubernetes(config);

            var pods = client.CoreV1.ListNamespacedPod("grpc-service-account");

            return Task.FromResult(new ApplicationInformationResponse()
            {
                // Change this logic to get application name and application version
                ApplicationName = $"{pods.Items[0].Spec.NodeName}",
                ApplicationVersion = $"{pods.Items[0].ApiVersion}",
            });
        }

        public override Task WatchResources(WatchResourcesRequest request, IServerStreamWriter<WatchResourcesUpdate> responseStream, ServerCallContext context)
        {
            return base.WatchResources(request, responseStream, context);
        }

        public override Task WatchResourceConsoleLogs(WatchResourceConsoleLogsRequest request, IServerStreamWriter<WatchResourceConsoleLogsUpdate> responseStream, ServerCallContext context)
        {
            return base.WatchResourceConsoleLogs(request, responseStream, context);
        }

        public override Task<ResourceCommandResponse> ExecuteResourceCommand(ResourceCommandRequest request, ServerCallContext context)
        {
            return base.ExecuteResourceCommand(request, context);
        }

    }
}
