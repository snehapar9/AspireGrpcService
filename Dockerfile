FROM mcr.microsoft.com/dotnet/aspnet:8.0-nanoserver-1809 AS base
WORKDIR /app
EXPOSE 5100

ENV ASPNETCORE_URLS=http://+:5100

FROM mcr.microsoft.com/dotnet/sdk:8.0-nanoserver-1809 AS build
ARG configuration=Release
WORKDIR /src
COPY ["AspireGrpcService.csproj", "./"]
RUN dotnet restore "AspireGrpcService.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet build "AspireGrpcService.csproj" -c $configuration -o /app/build

FROM build AS publish
ARG configuration=Release
RUN dotnet publish "AspireGrpcService.csproj" -c $configuration -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "AspireGrpcService.dll"]
