// <auto-generated>
//     Generated by the protocol buffer compiler.  DO NOT EDIT!
//     source: Protos/aspire.proto
// </auto-generated>
// Original file comments:
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
//
#pragma warning disable 0414, 1591, 8981, 0612
#region Designer generated code

using grpc = global::Grpc.Core;

namespace Aspire.V1 {
  public static partial class DashboardService
  {
    static readonly string __ServiceName = "aspire.v1.DashboardService";

    [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
    static void __Helper_SerializeMessage(global::Google.Protobuf.IMessage message, grpc::SerializationContext context)
    {
      #if !GRPC_DISABLE_PROTOBUF_BUFFER_SERIALIZATION
      if (message is global::Google.Protobuf.IBufferMessage)
      {
        context.SetPayloadLength(message.CalculateSize());
        global::Google.Protobuf.MessageExtensions.WriteTo(message, context.GetBufferWriter());
        context.Complete();
        return;
      }
      #endif
      context.Complete(global::Google.Protobuf.MessageExtensions.ToByteArray(message));
    }

    [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
    static class __Helper_MessageCache<T>
    {
      public static readonly bool IsBufferMessage = global::System.Reflection.IntrospectionExtensions.GetTypeInfo(typeof(global::Google.Protobuf.IBufferMessage)).IsAssignableFrom(typeof(T));
    }

    [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
    static T __Helper_DeserializeMessage<T>(grpc::DeserializationContext context, global::Google.Protobuf.MessageParser<T> parser) where T : global::Google.Protobuf.IMessage<T>
    {
      #if !GRPC_DISABLE_PROTOBUF_BUFFER_SERIALIZATION
      if (__Helper_MessageCache<T>.IsBufferMessage)
      {
        return parser.ParseFrom(context.PayloadAsReadOnlySequence());
      }
      #endif
      return parser.ParseFrom(context.PayloadAsNewBuffer());
    }

    [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
    static readonly grpc::Marshaller<global::Aspire.V1.ApplicationInformationRequest> __Marshaller_aspire_v1_ApplicationInformationRequest = grpc::Marshallers.Create(__Helper_SerializeMessage, context => __Helper_DeserializeMessage(context, global::Aspire.V1.ApplicationInformationRequest.Parser));
    [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
    static readonly grpc::Marshaller<global::Aspire.V1.ApplicationInformationResponse> __Marshaller_aspire_v1_ApplicationInformationResponse = grpc::Marshallers.Create(__Helper_SerializeMessage, context => __Helper_DeserializeMessage(context, global::Aspire.V1.ApplicationInformationResponse.Parser));
    [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
    static readonly grpc::Marshaller<global::Aspire.V1.WatchResourcesRequest> __Marshaller_aspire_v1_WatchResourcesRequest = grpc::Marshallers.Create(__Helper_SerializeMessage, context => __Helper_DeserializeMessage(context, global::Aspire.V1.WatchResourcesRequest.Parser));
    [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
    static readonly grpc::Marshaller<global::Aspire.V1.WatchResourcesUpdate> __Marshaller_aspire_v1_WatchResourcesUpdate = grpc::Marshallers.Create(__Helper_SerializeMessage, context => __Helper_DeserializeMessage(context, global::Aspire.V1.WatchResourcesUpdate.Parser));
    [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
    static readonly grpc::Marshaller<global::Aspire.V1.WatchResourceConsoleLogsRequest> __Marshaller_aspire_v1_WatchResourceConsoleLogsRequest = grpc::Marshallers.Create(__Helper_SerializeMessage, context => __Helper_DeserializeMessage(context, global::Aspire.V1.WatchResourceConsoleLogsRequest.Parser));
    [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
    static readonly grpc::Marshaller<global::Aspire.V1.WatchResourceConsoleLogsUpdate> __Marshaller_aspire_v1_WatchResourceConsoleLogsUpdate = grpc::Marshallers.Create(__Helper_SerializeMessage, context => __Helper_DeserializeMessage(context, global::Aspire.V1.WatchResourceConsoleLogsUpdate.Parser));
    [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
    static readonly grpc::Marshaller<global::Aspire.V1.ResourceCommandRequest> __Marshaller_aspire_v1_ResourceCommandRequest = grpc::Marshallers.Create(__Helper_SerializeMessage, context => __Helper_DeserializeMessage(context, global::Aspire.V1.ResourceCommandRequest.Parser));
    [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
    static readonly grpc::Marshaller<global::Aspire.V1.ResourceCommandResponse> __Marshaller_aspire_v1_ResourceCommandResponse = grpc::Marshallers.Create(__Helper_SerializeMessage, context => __Helper_DeserializeMessage(context, global::Aspire.V1.ResourceCommandResponse.Parser));

    [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
    static readonly grpc::Method<global::Aspire.V1.ApplicationInformationRequest, global::Aspire.V1.ApplicationInformationResponse> __Method_GetApplicationInformation = new grpc::Method<global::Aspire.V1.ApplicationInformationRequest, global::Aspire.V1.ApplicationInformationResponse>(
        grpc::MethodType.Unary,
        __ServiceName,
        "GetApplicationInformation",
        __Marshaller_aspire_v1_ApplicationInformationRequest,
        __Marshaller_aspire_v1_ApplicationInformationResponse);

    [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
    static readonly grpc::Method<global::Aspire.V1.WatchResourcesRequest, global::Aspire.V1.WatchResourcesUpdate> __Method_WatchResources = new grpc::Method<global::Aspire.V1.WatchResourcesRequest, global::Aspire.V1.WatchResourcesUpdate>(
        grpc::MethodType.ServerStreaming,
        __ServiceName,
        "WatchResources",
        __Marshaller_aspire_v1_WatchResourcesRequest,
        __Marshaller_aspire_v1_WatchResourcesUpdate);

    [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
    static readonly grpc::Method<global::Aspire.V1.WatchResourceConsoleLogsRequest, global::Aspire.V1.WatchResourceConsoleLogsUpdate> __Method_WatchResourceConsoleLogs = new grpc::Method<global::Aspire.V1.WatchResourceConsoleLogsRequest, global::Aspire.V1.WatchResourceConsoleLogsUpdate>(
        grpc::MethodType.ServerStreaming,
        __ServiceName,
        "WatchResourceConsoleLogs",
        __Marshaller_aspire_v1_WatchResourceConsoleLogsRequest,
        __Marshaller_aspire_v1_WatchResourceConsoleLogsUpdate);

    [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
    static readonly grpc::Method<global::Aspire.V1.ResourceCommandRequest, global::Aspire.V1.ResourceCommandResponse> __Method_ExecuteResourceCommand = new grpc::Method<global::Aspire.V1.ResourceCommandRequest, global::Aspire.V1.ResourceCommandResponse>(
        grpc::MethodType.Unary,
        __ServiceName,
        "ExecuteResourceCommand",
        __Marshaller_aspire_v1_ResourceCommandRequest,
        __Marshaller_aspire_v1_ResourceCommandResponse);

    /// <summary>Service descriptor</summary>
    public static global::Google.Protobuf.Reflection.ServiceDescriptor Descriptor
    {
      get { return global::Aspire.V1.AspireReflection.Descriptor.Services[0]; }
    }

    /// <summary>Base class for server-side implementations of DashboardService</summary>
    [grpc::BindServiceMethod(typeof(DashboardService), "BindService")]
    public abstract partial class DashboardServiceBase
    {
      [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
      public virtual global::System.Threading.Tasks.Task<global::Aspire.V1.ApplicationInformationResponse> GetApplicationInformation(global::Aspire.V1.ApplicationInformationRequest request, grpc::ServerCallContext context)
      {
        throw new grpc::RpcException(new grpc::Status(grpc::StatusCode.Unimplemented, ""));
      }

      [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
      public virtual global::System.Threading.Tasks.Task WatchResources(global::Aspire.V1.WatchResourcesRequest request, grpc::IServerStreamWriter<global::Aspire.V1.WatchResourcesUpdate> responseStream, grpc::ServerCallContext context)
      {
        throw new grpc::RpcException(new grpc::Status(grpc::StatusCode.Unimplemented, ""));
      }

      [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
      public virtual global::System.Threading.Tasks.Task WatchResourceConsoleLogs(global::Aspire.V1.WatchResourceConsoleLogsRequest request, grpc::IServerStreamWriter<global::Aspire.V1.WatchResourceConsoleLogsUpdate> responseStream, grpc::ServerCallContext context)
      {
        throw new grpc::RpcException(new grpc::Status(grpc::StatusCode.Unimplemented, ""));
      }

      [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
      public virtual global::System.Threading.Tasks.Task<global::Aspire.V1.ResourceCommandResponse> ExecuteResourceCommand(global::Aspire.V1.ResourceCommandRequest request, grpc::ServerCallContext context)
      {
        throw new grpc::RpcException(new grpc::Status(grpc::StatusCode.Unimplemented, ""));
      }

    }

    /// <summary>Creates service definition that can be registered with a server</summary>
    /// <param name="serviceImpl">An object implementing the server-side handling logic.</param>
    [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
    public static grpc::ServerServiceDefinition BindService(DashboardServiceBase serviceImpl)
    {
      return grpc::ServerServiceDefinition.CreateBuilder()
          .AddMethod(__Method_GetApplicationInformation, serviceImpl.GetApplicationInformation)
          .AddMethod(__Method_WatchResources, serviceImpl.WatchResources)
          .AddMethod(__Method_WatchResourceConsoleLogs, serviceImpl.WatchResourceConsoleLogs)
          .AddMethod(__Method_ExecuteResourceCommand, serviceImpl.ExecuteResourceCommand).Build();
    }

    /// <summary>Register service method with a service binder with or without implementation. Useful when customizing the service binding logic.
    /// Note: this method is part of an experimental API that can change or be removed without any prior notice.</summary>
    /// <param name="serviceBinder">Service methods will be bound by calling <c>AddMethod</c> on this object.</param>
    /// <param name="serviceImpl">An object implementing the server-side handling logic.</param>
    [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
    public static void BindService(grpc::ServiceBinderBase serviceBinder, DashboardServiceBase serviceImpl)
    {
      serviceBinder.AddMethod(__Method_GetApplicationInformation, serviceImpl == null ? null : new grpc::UnaryServerMethod<global::Aspire.V1.ApplicationInformationRequest, global::Aspire.V1.ApplicationInformationResponse>(serviceImpl.GetApplicationInformation));
      serviceBinder.AddMethod(__Method_WatchResources, serviceImpl == null ? null : new grpc::ServerStreamingServerMethod<global::Aspire.V1.WatchResourcesRequest, global::Aspire.V1.WatchResourcesUpdate>(serviceImpl.WatchResources));
      serviceBinder.AddMethod(__Method_WatchResourceConsoleLogs, serviceImpl == null ? null : new grpc::ServerStreamingServerMethod<global::Aspire.V1.WatchResourceConsoleLogsRequest, global::Aspire.V1.WatchResourceConsoleLogsUpdate>(serviceImpl.WatchResourceConsoleLogs));
      serviceBinder.AddMethod(__Method_ExecuteResourceCommand, serviceImpl == null ? null : new grpc::UnaryServerMethod<global::Aspire.V1.ResourceCommandRequest, global::Aspire.V1.ResourceCommandResponse>(serviceImpl.ExecuteResourceCommand));
    }

  }
}
#endregion
