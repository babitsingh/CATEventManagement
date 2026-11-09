using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Cosmos;

namespace CatEventManagement.Endpoints;


public static class CatEventEndpoints
{
    // Extension method to map all cat event routes
    public static IEndpointRouteBuilder MapCatEventEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/cat-events/publish", async (CatEventPayload payload,
        CosmosClient cosmosClient,
            ServiceBusClient serviceBusClient,
            IConfiguration configuration) =>
        {if (string.IsNullOrWhiteSpace(payload.HtmlContent))
            {
                return Results.BadRequest(new { error = "HtmlContent payload cannot be empty." });
            }

            // 1. Prepare Cosmos DB Entity (Stores full rich HTML payload)
            var dbName = configuration["AzureCosmos:DatabaseName"] ?? "CatDatabase";
            var containerName = configuration["AzureCosmos:ContainerName"] ?? "CatEvents";
            var container = cosmosClient.GetContainer(dbName, containerName);

            var eventDocument = new CatEventDocument
            {
                id = Guid.NewGuid().ToString(),
                PerilsId = Guid.NewGuid().ToString(), // Assuming you want a unique PerilsId as well
                Title = payload.Title,  
                HtmlContent = payload.HtmlContent,
                CreatedAt = DateTime.UtcNow
            };
try
{
   // Save full record to Cosmos DB (partitioned by Id)
            await container.CreateItemAsync(eventDocument, new PartitionKey(eventDocument.PerilsId));
}
catch (CosmosException ex)
{
    Console.WriteLine($"Status: {ex.StatusCode}");
    Console.WriteLine($"SubStatus: {ex.SubStatusCode}");
    Console.WriteLine($"Message: {ex.Message}");

    throw;
}
           

            // 2. Prepare Lighter Summary Message for Azure Service Bus Topic
            var topicName = configuration["AzureServiceBus:TopicName"] ?? "cat-event-updates-topic";
            var sender = serviceBusClient.CreateSender(topicName);

            var summaryMessage = new CatEventSummaryMessage(
                EventId: eventDocument.id,
                Title: eventDocument.Title,
                CreatedAt: eventDocument.CreatedAt
            );

            // Serialize lighter payload and send message to Topic
            var messageBody = JsonSerializer.Serialize(summaryMessage);
            var serviceBusMessage = new ServiceBusMessage(messageBody)
            {
                ContentType = "application/json",
                Subject = "CatEventPublished"
            };

            await sender.SendMessageAsync(serviceBusMessage);

            return Results.Ok(new 
            {
                message = "Cat event stored in Cosmos DB and broadcasted to Service Bus successfully!",
                eventId = eventDocument.id,
                timestamp = eventDocument.CreatedAt
            });
        })
        .WithName("PublishCatEventUpldates")
        .Produces<object>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest);

        return app;
    }
}

// Request payload model can also live here or in a separate Models folder
public record CatEventPayload(string Title, string HtmlContent);

// Full model stored in Cosmos DB (contains heavy HTML content)
public record CatEventDocument
{
    [JsonPropertyName("id")]
    public string id { get;init; } = null!;

     [JsonPropertyName("PerilsId")]
    public string PerilsId { get;init; } = null!;
    
    [JsonPropertyName("title")]
    public string Title { get;init; } = null!;
    
    [JsonPropertyName("htmlContent")]
    public string HtmlContent { get;init; } = null!;
    
    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get;init; }
}

// Lighter summary model sent to Service Bus (omits heavy HTML payload)
public record CatEventSummaryMessage(
    string EventId,
    string Title,
    DateTime CreatedAt
);