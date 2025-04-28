using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using SemKernel.Plugins;

const string modelId = "not-needed";
var endpoint = new Uri("http://localhost:3000/v1");
const string apiKey = "-";

#pragma warning disable SKEXP0070
var builder = Kernel.CreateBuilder().AddOpenAIChatCompletion(modelId: modelId, endpoint: endpoint, apiKey: apiKey);
builder.Services.AddLogging(services => services.AddConsole().SetMinimumLevel(LogLevel.Trace));
builder.Plugins.AddFromFunctions("component_execution", [
    KernelFunctionFactory.CreateFromMethod(
        method: (ComponentExecutionPlugin plugin) => plugin.ExecuteComponent,
        functionName: "isolate_machine",
        description: "Isolates a machine from the network"
    ),
    KernelFunctionFactory.CreateFromMethod(
        method: (ComponentExecutionPlugin plugin) => plugin.ExecuteComponent,
        functionName: "send_slack_notification",
        description: "Sends a slack notification to the designated channel"
    )
]);

var kernel = builder.Build();
var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();
var history = new ChatHistory();

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
        kernel: kernel);

    // Print the results
    Console.WriteLine("Assistant > " + result);

    // Add the message from the agent to the chat history
    history.AddMessage(result.Role, result.Content ?? string.Empty);
} while (userInput is not null);