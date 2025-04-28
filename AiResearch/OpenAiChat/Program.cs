// See https://aka.ms/new-console-template for more information

using System.ClientModel;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;

const string model = "not-needed";
var endpoint = new Uri("http://localhost:3000/v1");
const string apiKey = "-";

var client = new OpenAIClient(new ApiKeyCredential(apiKey), new OpenAIClientOptions
{
    Endpoint = endpoint
});

var chat = client.GetChatClient(model).AsIChatClient();
List<ChatMessage> chatHistory = [];

string? userPrompt;
do
{
    Console.WriteLine("Your prompt: ");
    userPrompt = Console.ReadLine();
    chatHistory.Add(new ChatMessage(ChatRole.User, userPrompt));
    
    Console.WriteLine("Response:");
    var response = string.Empty;
    await foreach (var item in chat.GetStreamingResponseAsync(chatHistory))
    {
        Console.Write(item.Text);
        response += item.Text;
    }
    chatHistory.Add(new ChatMessage(ChatRole.Assistant, response));
    Console.WriteLine();
}
while (!string.IsNullOrEmpty(userPrompt));