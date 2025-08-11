using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.MistralAI;
using Microsoft.SemanticKernel.Connectors.MistralAI.Client;
using ModelContextProtocol.Client;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using SemKernel.Filters;
using SemKernel.Plugins;

const string modelId = "/root/.cache/huggingface/hub/models--mistralai--Mistral-Small-3.1-24B-Instruct-2503/snapshots/73ce7c62b904fa83d7cb018e44c3bc06feed4d81";
var endpoint = new Uri("http://localhost:8001/v1/");
const string apiKey = "-";

var resourceBuilder = ResourceBuilder
    .CreateDefault()
    .AddService("SemKernel");

using var traceProvider = Sdk.CreateTracerProviderBuilder()
    .SetResourceBuilder(resourceBuilder)
    .AddSource("Microsoft.SemanticKernel*")
    //.AddConsoleExporter()
    .Build();

AppContext.SetSwitch("Microsoft.SemanticKernel.Experimental.GenAI.EnableOTelDiagnosticsSensitive", true);

using var loggerFactory = LoggerFactory.Create(builder =>
{
    // Add OpenTelemetry as a logging provider
    builder.AddOpenTelemetry(options =>
    {
        options.SetResourceBuilder(resourceBuilder);
        options.AddConsoleExporter();
        // Format log messages. This is default to false.
        options.IncludeFormattedMessage = true;
        options.IncludeScopes = true;
    });
    builder.SetMinimumLevel(LogLevel.Information);
});
     

#pragma warning disable SKEXP0070
var builder = Kernel.CreateBuilder().AddMistralChatCompletion(modelId: modelId, endpoint: endpoint, apiKey: apiKey);
builder.Services.AddLogging(services => services.AddConsole().SetMinimumLevel(LogLevel.Trace));
builder.Plugins.AddFromFunctions("component_execution", [
    KernelFunctionFactory.CreateFromMethod(
        method: (KernelArguments args) =>
        {
            Console.WriteLine("City: " + args.GetValueOrDefault("city"));
            Console.WriteLine("State: " + args.GetValueOrDefault("state"));
            return "42 degrees";
        },
        functionName: "get_weather",
        description: "Gets the weather for a location, Boston, MA for example",
        parameters: [
            new KernelParameterMetadata("city") { Description = "The city to get the weather for" },
            new KernelParameterMetadata("state") { Description = "The state to get the weather for" },
        ]
    )
]);

const string accountId = "98d1baca-8a60-4556-a1cb-7a5d2de705f2";
const string tenantId = "3b632b7d-3dac-4ce7-aaab-747941733ae2";
const string jwt = "eyInvalidhbGciOiJFUzI1NiIsInR5cCI6IkpXVCJ9.eyJuYW1laWQiOiJjODI1MzNmMi0zMTZjLTRlYmUtYjgxYy0wZTk5NDUxNDhjYWIiLCJ1bmlxdWVfbmFtZSI6InN1cGVyYWRtaW5Ac3dpbWxhbmUuY29tIiwiQWNjb3VudElkIjoiOThkMWJhY2EtOGE2MC00NTU2LWExY2ItN2E1ZDJkZTcwNWYyIiwianRpIjoiZDAxMGU1NmItZmU0Mi00YjEwLTkwMjctY2RkODEyN2FhNDhkIiwiZ2l2ZW5fbmFtZSI6InN1cGVyYWRtaW4iLCJlbWFpbCI6InN1cGVyYWRtaW5Ac3dpbWxhbmUuY29tIiwic2NvcGUiOiJleGVjdXRlOiogcmVhZDoqIiwibmJmIjoxNzU0OTIxMjA0LCJleHAiOjE3NTQ5MzU2MDQsImlhdCI6MTc1NDkyMTIwNCwiaXNzIjoiU3dpbWxhbmUiLCJhdWQiOiJTd2ltbGFuZSJ9.eQRPuxmO3hc_Gl294JuzkkhtLUETM2e9noKIjIgxLM_KLJqXnVpnB5_8idYm2Gb0kVokaD4oBE1Q0aMkPDNOxQ";

var mcpClient = await McpClientFactory.CreateAsync(
    new SseClientTransport(
        new SseClientTransportOptions
        {
            Endpoint = new Uri($"http://localhost:5004/account/{accountId}/tenant/{tenantId}/server/default"),
            AdditionalHeaders = new Dictionary<string, string>
            {
                ["Cookie"] = $"Jwt={jwt}"
            }
        }));

#pragma warning disable SKEXP0001
var mcpTools = (await mcpClient.ListToolsAsync()).Select(tool => tool.AsKernelFunction());
#pragma warning restore SKEXP0001

builder.Plugins.AddFromFunctions("mcp", mcpTools);

//builder.Plugins.AddFromType<TestPlugin>();

builder.Services.AddSingleton(loggerFactory);
builder.Services.AddSingleton<IFunctionInvocationFilter, MyFunctionInvocationFilter>();

var kernel = builder.Build();
var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();
var history = new ChatHistory();
history.AddSystemMessage("You are a helpful assistant");

var executionSettings = new MistralAIPromptExecutionSettings
{
    FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
    ToolCallBehavior = MistralAIToolCallBehavior.EnableKernelFunctions
};

// Initiate a back-and-forth chat
string? userInput;
do {
    // Collect user input
    Console.Write("User > ");
    userInput = Console.ReadLine();

    // Add user input
    history.AddUserMessage(userInput!);
    
    // Get the response from the AI
    var result = await chatCompletionService.GetChatMessageContentAsync(
        history,
        executionSettings: executionSettings,
        kernel: kernel);

    var functionCalls = FunctionCallContent.GetFunctionCalls(result);
    foreach (var functionCall in functionCalls)
    {
        var callResult = await functionCall.InvokeAsync(kernel);
        history.Add(callResult.ToChatMessage());
    }
    
    // Print the results
    Console.WriteLine("Assistant > " + result);
    
    // Print metadata
    /*if (result.Metadata != null && result.Metadata.TryGetValue("Usage", out var value))
    {
        var usage = (MistralUsage)value!;
        Console.WriteLine($"Input Token Count: {usage.PromptTokens ?? 0}");
        Console.WriteLine($"Output Token Count: {usage.CompletionTokens ?? 0}");
    }*/

    // Add the message from the agent to the chat history
    history.AddMessage(result.Role, result.Content ?? string.Empty);
} while (userInput is not null);