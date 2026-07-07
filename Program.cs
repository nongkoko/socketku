using soketku;

internal class Program
{
    private static void Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        //builder.Services.AddHostedService<Worker>();

        var aSocket = new soketku.soketku() as iSoketku;
        Console.WriteLine("connecting");
        aSocket.connect("localhost", 5678);
        Console.WriteLine("end of connecting");

        try
        {
            Console.WriteLine("ready");
            aSocket.send("hello world");
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            throw;
        }

        var host = builder.Build();
        host.Run();
    }
}