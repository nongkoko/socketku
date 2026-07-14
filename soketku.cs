using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;

namespace soketku;

public interface iTCPheader
{
    bool headerMSBfirst { get; }
    bool lengthIncludeHeader { get; }
    bool lengthIncludeTailer { get; }
    byte[]? trailer { get; }
}

public interface iSoketku
{
    void connect(string ipAddress, int port);
    void send(string dataToSend);
    Task listenAsync(int port);
    string connName { get; set; }
    iTCPheader tcpHeader { get; set; }
    event Action<string, byte[]> dataReceived;
}

internal class soketku : iSoketku
{
    private Socket _socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
    string iSoketku.connName { get; set; }
    iTCPheader iSoketku.tcpHeader { get; set; }
    private Action<string, byte[]>? _dlgDataReceived = null;

    event Action<string, byte[]> iSoketku.dataReceived
    {
        add
        {
            _dlgDataReceived += value;
        }

        remove
        {
            _dlgDataReceived -= value;
        }
    }

    void iSoketku.connect(string ipAddress, int port)
    {
        _socket.Connect(ipAddress, port);

        //kalau sudah connect, maka jalankan task untuk menerima data
        Task.Run(async () =>
        {
            var thisAsIsocketKu = (iSoketku)this;
            var mainBuffer = new byte[32767];
            var aBuffer = new byte[16384];
            var totalDataRead = 0;
            var currentByteRead = 0;

            while ((currentByteRead = await _socket.ReceiveAsync(aBuffer)) > 0)
            {
                if (thisAsIsocketKu.tcpHeader == null)
                {
                    var theString = System.Text.Encoding.UTF8.GetString(aBuffer, 0, currentByteRead);
                    var theBytes = new byte[currentByteRead];
                    Buffer.BlockCopy(aBuffer, 0, theBytes, 0, currentByteRead);
                    _dlgDataReceived?.Invoke(theString, theBytes);
                    continue;
                }

                Buffer.BlockCopy(aBuffer, 0, mainBuffer, totalDataRead, currentByteRead);
                totalDataRead += currentByteRead;

                //get expected length
                var dataLength = (ushort)0;
                if (thisAsIsocketKu.tcpHeader.headerMSBfirst)
                    dataLength = BinaryPrimitives.ReadUInt16BigEndian(aBuffer);
                else
                    dataLength = BinaryPrimitives.ReadUInt16LittleEndian(aBuffer);

                var oneBlockLength = 0 + dataLength;

                //menentukan 1 block
                if (!thisAsIsocketKu.tcpHeader.headerMSBfirst)
                    oneBlockLength += 2;

                if (!thisAsIsocketKu.tcpHeader.lengthIncludeTailer)
                    oneBlockLength += (ushort)thisAsIsocketKu.tcpHeader.trailer.Length;

                if (totalDataRead >= oneBlockLength)
                {
                    var payloadOnly = Encoding.UTF8.GetString(mainBuffer, 2, dataLength);
                    var incomingDataAsIs = new byte[oneBlockLength];
                    Buffer.BlockCopy(mainBuffer, 0, incomingDataAsIs, 0, oneBlockLength);

                    _dlgDataReceived?.Invoke(payloadOnly, incomingDataAsIs);

                    totalDataRead -= oneBlockLength;
                    if (totalDataRead > 0)
                        Buffer.BlockCopy(mainBuffer, oneBlockLength, mainBuffer, 0, totalDataRead);
                }

            }
        });
    }

    private TcpListener? _tcpListener = null;
    async Task iSoketku.listenAsync(int port)
    {
        var alreadyListen = _tcpListener != null;
        if (alreadyListen)
            return;

        _tcpListener = new TcpListener(System.Net.IPAddress.Any, port);
        _tcpListener.Start();
        while (true)
        {
            var client = await _tcpListener.AcceptTcpClientAsync();
            _ = handleClientAsync(client);
        }
    }

    private async Task handleClientAsync(TcpClient client)
    {
        var stream = client.GetStream();
        var buffer = new byte[16384];
        int bytesRead;
        while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
        {
            var receivedData = System.Text.Encoding.UTF8.GetString(buffer, 0, bytesRead);
        }
    }

    void iSoketku.send(string dataToSend)
    {
        var thisAsiSoket = (iSoketku)this;
        var payload = System.Text.Encoding.UTF8.GetBytes(dataToSend);

        if (thisAsiSoket.tcpHeader == null)
        {
            _socket.Send(payload, 0, payload.Length, SocketFlags.None);
            return;
        }

        var buffer = new byte[5000];
        var panjangAsHeader = (ushort)payload.Length;
        var totalByteToSend = 0;
        var writePointer = 0;

        //menulis 2 byte ke buffer
        if (thisAsiSoket.tcpHeader.headerMSBfirst)
            BinaryPrimitives.WriteUInt16BigEndian(buffer, panjangAsHeader);
        else
            BinaryPrimitives.WriteUInt16LittleEndian(buffer, panjangAsHeader);
        totalByteToSend += 2;
        writePointer += 2;

        //menulis payload ke buffer
        System.Buffer.BlockCopy(payload, 0, buffer, writePointer, panjangAsHeader);
        totalByteToSend += panjangAsHeader;
        writePointer += panjangAsHeader;

        //mengirim buffer
        _socket.Send(buffer, 0, totalByteToSend, SocketFlags.None);
    }
}