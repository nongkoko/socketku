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
    private bool _headerBigEndian;

    string iSoketku.connName => _connName;

    void iSoketku.setup(string connName, bool headerBigEndian)
    {
        _connName = connName;
        _headerBigEndian = headerBigEndian;
    }

    void iSoketku.connect(string ipAddress, int port)
    {
        _socket.Connect(ipAddress, port);


        Task.Run(async () =>
        {
            var aBuffer = new byte[16384];
            var jumlahByteRead = 0;
            var panjangTotal = (ushort)0;
            while ((jumlahByteRead = await _socket.ReceiveAsync(aBuffer)) > 0)
            {
                if (_headerBigEndian)
                    panjangTotal = BinaryPrimitives.ReadUInt16BigEndian(aBuffer);
                else
                    panjangTotal = BinaryPrimitives.ReadUInt16LittleEndian(aBuffer);

                if (jumlahByteRead > panjangTotal)
                {
                    var theString = System.Text.Encoding.UTF8.GetString(aBuffer, 0, jumlahByteRead);
                    //whatHappen?.Invoke(theString);
                }

            }
        });
    }

    void iSoketku.send(string dataToSend)
    {
        var buffer = new byte[5000];
        var payload = System.Text.Encoding.UTF8.GetBytes(dataToSend);
        var panjangAsHeader = (ushort)payload.Length;
        var totalByteToSend = 0;
        var writePointer = 0;

        //menulis 2 byte ke buffer
        if (_headerBigEndian)
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