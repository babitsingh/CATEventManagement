using CatEventManagement.Endpoints;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Cosmos;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
// Register Azure Cosmos Client
builder.Services.AddSingleton<CosmosClient>(sp =>
{
    var connectionString = builder.Configuration.GetConnectionString("CosmosDb")!;
    return new CosmosClient(connectionString);
});

// Register Azure Service Bus Client
builder.Services.AddSingleton<ServiceBusClient>(sp =>
{
    var connectionString = builder.Configuration.GetConnectionString("ServiceBus")!;
    return new ServiceBusClient(connectionString);
});
// Add Swagger generation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    // Enable Swagger middleware
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "CatEventManagement v1");
        c.RoutePrefix = string.Empty; // Optional: Makes Swagger UI load directly at root http://localhost:xxxx/
    });
}

app.UseHttpsRedirection();


app.MapCatEventEndpoints(); // Map the cat event endpoints defined in EndPoints.cs

app.Run();

