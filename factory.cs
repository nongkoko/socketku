using System.Net.Sockets;

namespace soketku;

public class factory
{
    public static iSoketku newSoket()
    {
        return new soketku();
    }

    public static async IAsyncEnumerable<iSoketku> listenAh(int port)
    {

        var tcpListener = new TcpListener(System.Net.IPAddress.Any, port);
        tcpListener.Start();
        while (true)
        {
            var client = await tcpListener.AcceptTcpClientAsync();
            var aClient = new soketku(client);
            yield return aClient;
        }
    }
}
