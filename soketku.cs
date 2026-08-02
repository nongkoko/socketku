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
    void sendRaw(byte[] dataToSend);
    string connName { get; set; }
    iTCPheader tcpHeader { get; set; }
    event Action<iSoketku, string, byte[]> dataReceived;
    event Action<iSoketku> disconnected;
    Task startReadDataFromStream();
    bool isConnected { get; }
}

internal class soketku : iSoketku
{
    private iSoketku _asSoketKu => this;
    private Socket _socket;
    string iSoketku.connName { get; set; }
    iTCPheader iSoketku.tcpHeader { get; set; }
    bool iSoketku.isConnected => _isConnected;
    private Action<iSoketku, string, byte[]>? _dlgDataReceived;
    private Action<iSoketku> _dlgDisconnected;
    private bool _isConnected;
    event Action<iSoketku, string, byte[]> iSoketku.dataReceived
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

    event Action<iSoketku> iSoketku.disconnected
    {
        add
        {
            _dlgDisconnected += value;
        }

        remove
        {
            _dlgDisconnected -= value;
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
        var payload = Encoding.UTF8.GetBytes(dataToSend);

        if (_asSoketKu.tcpHeader == null)
        {
            _socket.Send(payload, 0, payload.Length, SocketFlags.None);
            return;
        }

        var buffer = new byte[5000];
        var panjangAsHeader = (ushort)payload.Length;
        var totalByteToSend = 0;
        var writePointer = 0;

        //menulis 2 byte ke buffer
        if (_asSoketKu.tcpHeader.headerMSBfirst)
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
        var mainBuffer = new byte[16384];
        var aBuffer = new byte[5000];
        var totalDataRead = 0;
        var currentByteRead = 0;

        while ((currentByteRead = await _socket.ReceiveAsync(aBuffer)) > 0)
        {
            if (_asSoketKu.tcpHeader == null)
            {
                var theString = Encoding.UTF8.GetString(aBuffer, 0, currentByteRead);
                var theBytes = new byte[currentByteRead];
                Buffer.BlockCopy(aBuffer, 0, theBytes, 0, currentByteRead);
                _dlgDataReceived?.Invoke(_asSoketKu, theString, theBytes);
                continue;
            }

            Buffer.BlockCopy(aBuffer, 0, mainBuffer, totalDataRead, currentByteRead);
            totalDataRead += currentByteRead;

            while (totalDataRead >= 2)
            {
                //get expected length
                var headerLength = (ushort)0;
                if (_asSoketKu.tcpHeader.headerMSBfirst)
                    headerLength = BinaryPrimitives.ReadUInt16BigEndian(mainBuffer);
                else
                    headerLength = BinaryPrimitives.ReadUInt16LittleEndian(mainBuffer);

                //menentukan 1 block
                var oneBlockLength = 0 + headerLength;
                if (!_asSoketKu.tcpHeader.lengthIncludeHeader)
                    oneBlockLength += 2;

                if (!_asSoketKu.tcpHeader.lengthIncludeTailer)
                    oneBlockLength += (ushort)(_asSoketKu.tcpHeader.trailer?.Length ?? 0);

                while (totalDataRead < oneBlockLength)
                {
                    currentByteRead = await _socket.ReceiveAsync(aBuffer);
                    Buffer.BlockCopy(aBuffer, 0, mainBuffer, totalDataRead, currentByteRead);
                    totalDataRead += currentByteRead;
                }

                var payloadLength = headerLength;
                if (_asSoketKu.tcpHeader.lengthIncludeHeader)
                    payloadLength -= 2;
                if (_asSoketKu.tcpHeader.lengthIncludeTailer)
                    payloadLength -= (ushort)(_asSoketKu.tcpHeader.trailer?.Length ?? 0);

                var payloadOnly = Encoding.UTF8.GetString(mainBuffer, 2, payloadLength);
                var incomingDataAsIs = new byte[oneBlockLength];
                Buffer.BlockCopy(mainBuffer, 0, incomingDataAsIs, 0, oneBlockLength);
                totalDataRead -= oneBlockLength;
                if (totalDataRead > 0)
                    Buffer.BlockCopy(mainBuffer, oneBlockLength, mainBuffer, 0, totalDataRead);
                _dlgDataReceived?.Invoke(_asSoketKu, payloadOnly, incomingDataAsIs);
            }
        }
        _isConnected = false;
        _dlgDisconnected?.Invoke(_asSoketKu);
    }

    void iSoketku.sendRaw(byte[] dataToSend)
    {
        _socket.Send(dataToSend, 0, dataToSend.Length, SocketFlags.None);
    }
}