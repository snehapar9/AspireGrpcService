using AspireGrpcService.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddGrpc();

var app = builder.Build();

builder.Services.AddGrpc();
builder.Services.AddGrpcReflection();


// Configure the HTTP request pipeline.
app.MapGrpcService<GreeterService>();
app.MapGrpcService<AspireService>();

// Enable reflection in Debug mode.
if (app.Environment.IsDevelopment())
{
    app.MapGrpcReflectionService();
}
app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");

app.Run();
