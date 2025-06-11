using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.MistralAI;
using Microsoft.SemanticKernel.Connectors.MistralAI.Client;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

const string modelId = "mistralai/Mistral-Small-3.1-24B-Instruct-2503";
var endpoint = new Uri("http://54.184.10.215:3000/v1/");
const string apiKey = "-";

var resourceBuilder = ResourceBuilder
    .CreateDefault()
    .AddService("SemKernel");

using var traceProvider = Sdk.CreateTracerProviderBuilder()
    .SetResourceBuilder(resourceBuilder)
    .AddSource("Microsoft.SemanticKernel*")
    .AddConsoleExporter()
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
    builder.SetMinimumLevel(LogLevel.Trace);
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

builder.Services.AddSingleton(loggerFactory);

var kernel = builder.Build();
var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();
var history = new ChatHistory();
history.AddSystemMessage("You are a helpful assistant");

var executionSettings = new MistralAIPromptExecutionSettings
{
    FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
    ToolCallBehavior = MistralAIToolCallBehavior.AutoInvokeKernelFunctions
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
    
    // Print the results
    Console.WriteLine("Assistant > " + result);
    
    // Print metadata
    if (result.Metadata != null && result.Metadata.TryGetValue("Usage", out var value))
    {
        var usage = (MistralUsage)value!;
        Console.WriteLine($"Input Token Count: {usage.PromptTokens ?? 0}");
        Console.WriteLine($"Output Token Count: {usage.CompletionTokens ?? 0}");
    }

    // Add the message from the agent to the chat history
    history.AddMessage(result.Role, result.Content ?? string.Empty);
} while (userInput is not null);