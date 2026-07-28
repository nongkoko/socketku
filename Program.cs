using soketku;

internal class Program
{
    private static async Task Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        //builder.Services.AddHostedService<Worker>();

        await foreach(var eachClient in factory.listenAh(9000))
        {
            eachClient.dataReceived += (connName, data) =>
            {
                Console.WriteLine($"data received from {connName}: {System.Text.Encoding.UTF8.GetString(data)}");
            };

            Console.WriteLine("connected");
            await Task.Delay(1000);
            eachClient.send("hello");
        }

        var host = builder.Build();
        host.Run();
    }
}