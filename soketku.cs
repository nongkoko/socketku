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
    string connName { get; set; }
    iTCPheader tcpHeader { get; set; }
    event Action<string, byte[]> dataReceived;
    Task startReadDataFromStream();
}

internal class soketku : iSoketku
{
    private Socket _socket;
    string iSoketku.connName { get; set; }
    iTCPheader iSoketku.tcpHeader { get; set; }
    private Action<string, byte[]>? _dlgDataReceived;
    private bool _isConnected;
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

    public soketku()
    {
        _socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
    }
    public soketku(TcpClient clientToHandle)
    {
        _socket = clientToHandle.Client;
    }

    void iSoketku.connect(string ipAddress, int port)
    {
        _socket.Connect(ipAddress, port);
        _isConnected = true;
    }

    void iSoketku.send(string dataToSend)
    {
        var thisAsiSoket = (iSoketku)this;
        var payload = Encoding.UTF8.GetBytes(dataToSend);

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

    async Task iSoketku.startReadDataFromStream()
    {
        var thisAsIsocketKu = (iSoketku)this;
        var mainBuffer = new byte[16384];
        var aBuffer = new byte[5000];
        var totalDataRead = 0;
        var currentByteRead = 0;

        while ((currentByteRead = await _socket.ReceiveAsync(aBuffer)) > 0)
        {
            if (thisAsIsocketKu.tcpHeader == null)
            {
                var theString = Encoding.UTF8.GetString(aBuffer, 0, currentByteRead);
                var theBytes = new byte[currentByteRead];
                Buffer.BlockCopy(aBuffer, 0, theBytes, 0, currentByteRead);
                _dlgDataReceived?.Invoke(theString, theBytes);
                continue;
            }

            Buffer.BlockCopy(aBuffer, 0, mainBuffer, totalDataRead, currentByteRead);
            totalDataRead += currentByteRead;

            while (totalDataRead >= 2)
            {
                //get expected length
                var headerLength = (ushort)0;
                if (thisAsIsocketKu.tcpHeader.headerMSBfirst)
                    headerLength = BinaryPrimitives.ReadUInt16BigEndian(mainBuffer);
                else
                    headerLength = BinaryPrimitives.ReadUInt16LittleEndian(mainBuffer);

                //menentukan 1 block
                var oneBlockLength = 0 + headerLength;
                if (!thisAsIsocketKu.tcpHeader.lengthIncludeHeader)
                    oneBlockLength += 2;

                if (!thisAsIsocketKu.tcpHeader.lengthIncludeTailer)
                    oneBlockLength += (ushort)(thisAsIsocketKu.tcpHeader.trailer?.Length ?? 0);

                while (totalDataRead < oneBlockLength)
                {
                    currentByteRead = await _socket.ReceiveAsync(aBuffer);
                    Buffer.BlockCopy(aBuffer, 0, mainBuffer, totalDataRead, currentByteRead);
                    totalDataRead += currentByteRead;
                }

                var payloadLength = headerLength;
                if (thisAsIsocketKu.tcpHeader.lengthIncludeHeader)
                    payloadLength -= 2;
                if (thisAsIsocketKu.tcpHeader.lengthIncludeTailer)
                    payloadLength -= (ushort)(thisAsIsocketKu.tcpHeader.trailer?.Length ?? 0);

                var payloadOnly = Encoding.UTF8.GetString(mainBuffer, 2, payloadLength);
                var incomingDataAsIs = new byte[oneBlockLength];
                Buffer.BlockCopy(mainBuffer, 0, incomingDataAsIs, 0, oneBlockLength);
                totalDataRead -= oneBlockLength;
                if (totalDataRead > 0)
                    Buffer.BlockCopy(mainBuffer, oneBlockLength, mainBuffer, 0, totalDataRead);
                _dlgDataReceived?.Invoke(payloadOnly, incomingDataAsIs);
            }
        }
        _isConnected = false;
    }
}