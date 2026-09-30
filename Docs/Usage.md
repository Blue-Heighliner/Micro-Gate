# Usage

Runnable examples of `MicroGatePortSource`, `MicroGatePeer`, and `MicroGateMonitor` in different situations. `Received` and `StateChanged` are `IObservable<T>`; the library references System.Reactive, so `Subscribe` accepts a lambda directly.

## List the available ports

```csharp
using BlueHeighliner.MicroGate;

IMicroGatePortSource ports = new MicroGatePortSource();

foreach (string name in await ports.GetPorts())
{
    Console.WriteLine(name);
}
```

On Linux these are `ttySLG*` (PCI/PCIe) and MicroGate `ttyUSB*` devices; on Windows they are the device names reported by the driver.

## Connect and send

```csharp
using BlueHeighliner.MicroGate;

await using IMicroGatePeer peer = new MicroGatePeerFactory().Create();
await peer.Start("ttySLG0", address: 0x01, remoteAddress: 0x03);
await peer.Send("Hello"u8.ToArray());
```

Every peer needs two different HDLC addresses: its own (`address`), which the remote peer sends its commands to, and the remote peer's (`remoteAddress`), which this peer sends its commands to. The remote peer must be started with them the other way round, here `0x03` and `0x01`. With no options the default settings apply: NRZ encoding, CRC-32-CCITT, and flag idle.

## Subscribe before connecting

```csharp
using System.Text;
using BlueHeighliner.MicroGate;

await using IMicroGatePeer peer = new MicroGatePeerFactory().Create();

peer.StateChanged.Subscribe(state => Console.WriteLine($"State: {state}"));
peer.Exceptions.Subscribe(exception => Console.WriteLine($"Error: {exception.Message}"));
peer.Receiver = data =>
{
    using (data)
    {
        Console.WriteLine(Encoding.UTF8.GetString(data.Memory.Span));
    }
};

await peer.Start("ttySLG0", address: 0x01, remoteAddress: 0x03);
```

Setting the receiver and subscribing first means no state change and no early frame is missed. `Receiver` is a delegate given each frame's data, in order, one call at a time, as an `IMemoryOwner<byte>` whose memory is exactly the data. The delegate owns it from then on: dispose it as soon as you have finished with the data, which returns the memory to the pool as early as possible, or keep it or hand it to another thread until then. The peer never disposes it after the call, so one that is never disposed is only garbage collected. The delegate runs on a task of the peer's own, so it may block, for example on `peer.Send`, without stalling the acknowledgement of received frames. Frames are acknowledged as they arrive whether or not the delegate has finished, so a delegate slower than the line lets data queue up in memory.

## Wait for the remote peer instead of sending requests

```csharp
using BlueHeighliner.MicroGate;

MicroGatePeerOptions options = new() { RetryInterval = null };

await using IMicroGatePeer peer = new MicroGatePeerFactory().Create();

using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(30));
await peer.Start("ttySLG0", 0x01, 0x03, options, timeout.Token);
```

With a `null` retry interval this side never sends a connection request and completes when the remote peer's request arrives. By default both sides send requests every second until answered, so either may be started first.

## Change how often requests are sent

```csharp
using BlueHeighliner.MicroGate;

MicroGatePeerOptions options = new() { RetryInterval = TimeSpan.FromMilliseconds(250) };

await using IMicroGatePeer peer = new MicroGatePeerFactory().Create();
await peer.Start("ttySLG0", 0x01, 0x03, options);
```

## Tune retransmission

```csharp
using BlueHeighliner.MicroGate;

MicroGatePeerOptions options = new() { RetransmitInterval = TimeSpan.FromMilliseconds(250) };

await using IMicroGatePeer peer = new MicroGatePeerFactory().Create();
await peer.Start("ttySLG0", 0x01, 0x03, options);

byte[] message = new byte[peer.MaxPayloadSize];
await peer.Send(message);
```

Sent data is kept until the remote peer acknowledges it and is sent again if the remote peer rejects a gap or nothing is acknowledged within the interval. Use a `null` interval to resend only on rejection. A payload can be at most `MaxPayloadSize` bytes (`MaxInfoField` in the options, 1500 by default), and up to `TransmitWindow` sends (1 by default, up to 7) can be unacknowledged before the next one waits.

## React to a disconnect

```csharp
peer.StateChanged.Subscribe(
    state => Console.WriteLine($"State: {state}"),
    () => Console.WriteLine("Peer is finished."));
```

`StateChanged` completes when the peer becomes `Disconnected`, whether the remote peer disconnected, the device was lost, or the peer was disposed.

## Enable the poll/final bit

```csharp
using BlueHeighliner.MicroGate;

MicroGatePeerOptions options = new() { DisablePollFinalBit = false };

await using IMicroGatePeer peer = new MicroGatePeerFactory().Create();
await peer.Start("ttySLG0", 0x01, 0x03, options);
```

`DisablePollFinalBit` is `true` by default, so every frame the station sends has the poll/final bit at 0, including acknowledgements to a peer frame that had it set. Set it to `false` for a remote peer that expects the bit to reflect the frame's actual role.

## Configure the device

```csharp
using BlueHeighliner.MicroGate;

MicroGatePeerOptions options = new()
{
    Link = new()
    {
        Encoding = MicroGateEncoding.NrziSpace,
        Crc = MicroGateCrc.Crc16Ccitt,
    },
    IdlePattern = MicroGateIdlePattern.Flags,
};

await using IMicroGatePeer peer = new MicroGatePeerFactory().Create();
await peer.Start("ttySLG0", 0x01, 0x03, options);
```

Both peers must agree on encoding, CRC, and idle pattern.

## Register with a container

```csharp
using BlueHeighliner.MicroGate;
using Microsoft.Extensions.DependencyInjection;

ServiceCollection services = new();
services.AddSingleton<IMicroGatePortSource, MicroGatePortSource>();
services.AddSingleton<IMicroGatePeerFactory, MicroGatePeerFactory>();
```

A peer can be started only once, so a service that needs links takes the `IMicroGatePeerFactory` and calls `Create` for each one, disposing each peer when done. Registering `IMicroGatePeer` itself is possible, but only as transient, and a container then tracks every peer it resolves until the container is disposed.

## Monitor a device without joining its connection

```csharp
using BlueHeighliner.MicroGate;

await using IMicroGateMonitor monitor = new MicroGateMonitorFactory().Create();

monitor.Received.Subscribe(frame => Console.WriteLine($"{frame.Kind} from 0x{frame.Address:X2}, {frame.Raw.Length} bytes"));

await monitor.Start("ttySLG0");
```

`monitor` never writes to the device: it reports every frame it sees, including the SABM, UA, DISC, DM, FRMR, RR, and RNR frames two other stations use to manage their own connection, not just their information frames. Options are the same physical layer settings as a peer's (`Encoding`, `Crc`, `HardwareAddressFilter`); there is nothing HDLC-layer to configure, since a monitor never forms a connection.

## Handle a frame the monitor could not decode

```csharp
monitor.Received.Subscribe(frame =>
{
    if (frame.Kind == MicroGateFrameKind.Malformed)
    {
        Console.WriteLine($"Could not decode {frame.Raw.Length} bytes: {frame.ErrorMessage}");
        return;
    }

    Console.WriteLine($"{frame.Kind}: {frame.Payload.Length} byte payload");
});
```

A frame that is too short, or whose control byte does not match a recognized kind, is still reported rather than dropped, with `Raw` holding its bytes as received.
