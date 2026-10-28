/**
 * MyAIAgent | Console Client
 *
 * Composes the interactive local client and its factory-managed Gateway connection.
 */

using Microsoft.Extensions.DependencyInjection;
using MyAIAgent.ConsoleClient;

if (args is ["--help"])
{
    Console.WriteLine("Usage: dotnet run --project src/Clients/MyAIAgent.ConsoleClient -- [--gateway <local Gateway URL>]");
    Console.WriteLine("Configure Gateway:BaseUrl in appsettings.json, or override with Gateway__BaseUrl or --gateway.");
    Console.WriteLine("Configure McpServer:Endpoint for the agent-creation default, or override with McpServer__Endpoint.");
    return 0;
}

ClientOptions options;
try
{
    options = ConsoleConfiguration.Load(args, AppContext.BaseDirectory);
}
catch (ArgumentException exception)
{
    Console.Error.WriteLine(exception.Message);
    return 2;
}
catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or FormatException or InvalidOperationException)
{
    Console.Error.WriteLine("Could not load client configuration. Check appsettings.json and the environment-specific settings file.");
    return 2;
}

var services = new ServiceCollection();
services.AddConsoleClient(options);
await using var provider = services.BuildServiceProvider();
using var application = new ConsoleApplication(provider.GetRequiredService<GatewayClient>(), Console.In, Console.Out, options);
ConsoleCancelEventHandler cancel = (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    application.Cancel();
};
Console.CancelKeyPress += cancel;
try
{
    await application.RunAsync();
}
finally
{
    Console.CancelKeyPress -= cancel;
}
return 0;
