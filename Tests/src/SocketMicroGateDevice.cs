namespace BlueHeighliner.MicroGate;

internal sealed class SocketMicroGateDevice(Socket socket) : IMicroGateDevice
{
    public static async Task<(SocketMicroGateDevice First, SocketMicroGateDevice Second)> CreatePair()
    {
        using TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();

        Socket first = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        Task<Socket> accepting = listener.AcceptSocketAsync();
        await first.ConnectAsync(new IPEndPoint(IPAddress.Loopback, ((IPEndPoint)listener.LocalEndpoint).Port));
        Socket second = await accepting;
        second.NoDelay = true;

        return (new SocketMicroGateDevice(first), new SocketMicroGateDevice(second));
    }

    public int Read(byte[] buffer)
    {
        try
        {
            byte[] header = new byte[2];
            if (!Fill(header, 2))
            {
                return 0;
            }

            int length = (header[0] << 8) | header[1];
            return Fill(buffer, length) ? length : 0;
        }
        catch (Exception exception) when (exception is SocketException or ObjectDisposedException)
        {
            return 0;
        }
    }

    public void Write(ReadOnlyMemory<byte> frame)
    {
        try
        {
            byte[] message = new byte[frame.Length + 2];
            message[0] = (byte)(frame.Length >> 8);
            message[1] = (byte)frame.Length;
            frame.CopyTo(message.AsMemory(2));
            socket.Send(message);
        }
        catch (Exception exception) when (exception is SocketException or ObjectDisposedException)
        {
            throw new IOException("Failed to write the frame to the device.", exception);
        }
    }

    public void DisableReceiver()
    {
        try
        {
            socket.Shutdown(SocketShutdown.Receive);
        }
        catch (Exception exception) when (exception is SocketException or ObjectDisposedException)
        {
        }
    }

    public void DisableTransmitter()
    {
        try
        {
            socket.Shutdown(SocketShutdown.Send);
        }
        catch (Exception exception) when (exception is SocketException or ObjectDisposedException)
        {
        }
    }

    public void Dispose() => socket.Dispose();

    private bool Fill(byte[] buffer, int count)
    {
        int total = 0;
        while (total < count)
        {
            int read = socket.Receive(buffer, total, count - total, SocketFlags.None);
            if (read == 0)
            {
                return false;
            }

            total += read;
        }

        return true;
    }
}
