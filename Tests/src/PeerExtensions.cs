namespace BlueHeighliner.MicroGate;

internal static class PeerExtensions
{
    extension(IMicroGatePeer peer)
    {
        public async ValueTask StartAndConnect(string portName, byte address, byte remoteAddress, MicroGatePeerOptions? options = null, CancellationToken cancellation = default)
        {
            await peer.Start(portName, options, cancellation).ConfigureAwait(false);
            await peer.Connect(address, remoteAddress, cancellation).ConfigureAwait(false);
        }
    }
}
