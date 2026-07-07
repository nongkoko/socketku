using System.Buffers.Binary;
using System.Net.Sockets;

namespace soketku;

public interface iSoketku
{
    void setup(string connName, bool headerMSBfirst);
    void connect(string ipAddress, int port);
    void send(string dataToSend);
    string connName { get; }
}

internal class soketku : iSoketku
{
    private Socket _socket = new System.Net.Sockets.Socket(SocketType.Stream, ProtocolType.Tcp);
    private string _connName;
    private bool _headerMSBfirst;

    string iSoketku.connName => _connName;

    void iSoketku.setup(string connName, bool headerMSBfirst)
    {
        _connName = connName;
        _headerMSBfirst = headerMSBfirst;
    }

    void iSoketku.connect(string ipAddress, int port)
    {
        _socket.Connect(ipAddress, port);
        Task.Run(async () =>
        {
            var aBuffer = new byte[16384];
            var jumlahByte = 0;
            while ((jumlahByte = await _socket.ReceiveAsync(aBuffer)) > 0)
            {
                var theString = System.Text.Encoding.UTF8.GetString(aBuffer, 0, jumlahByte);
                //whatHappen?.Invoke(theString);
            }
        });
    }

    void iSoketku.send(string dataToSend)
    {
        var buffer = new byte[5000];
        var payload = System.Text.Encoding.UTF8.GetBytes(dataToSend);
        var panjangPayload = (ushort)payload.Length;
        var totalByteToSend = 0;
        var writePointer = 0;

        //menulis 2 byte 
        if (_headerMSBfirst)
            BinaryPrimitives.WriteUInt16BigEndian(buffer, panjangPayload);
        else
            BinaryPrimitives.WriteUInt16LittleEndian(buffer, panjangPayload);
        totalByteToSend += 2;
        writePointer += 2;

        //menulis payload
        System.Buffer.BlockCopy(payload, 0, buffer, writePointer, panjangPayload);
        totalByteToSend += panjangPayload;
        writePointer += panjangPayload;

        _socket.Send(buffer, 0, totalByteToSend, SocketFlags.None);
    }
}