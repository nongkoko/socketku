using soketku;

internal class Program
{
    private static async Task Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        //builder.Services.AddHostedService<Worker>();

        await foreach (var eachClient in factory.listenAh(9000))
        {
            eachClient.tcpHeader = new mTCPheader(true, false, true, null);
            eachClient.dataReceived += (siapa, payload, data) =>
            {
                Console.WriteLine($"data received from {siapa}: {System.Text.Encoding.UTF8.GetString(data)}");
            };
            eachClient.startReadDataFromStream();
        }

        var host = builder.Build();
        host.Run();
    }
}
public record mTCPheader(
    bool headerMSBfirst,
    bool lengthIncludeHeader,
    bool lengthIncludeTailer,
    byte[]? trailer
) : iTCPheader;