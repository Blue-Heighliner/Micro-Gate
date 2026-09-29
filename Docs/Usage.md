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
await peer.Start("ttySLG0");
await peer.Send("Hello"u8.ToArray());
```

With no options the default settings apply: NRZ encoding, CRC-16-CCITT, flag idle, and HDLC address `0xFF`, which the remote peer must also use.

## Subscribe before connecting

```csharp
using System.Text;
using BlueHeighliner.MicroGate;

await using IMicroGatePeer peer = new MicroGatePeerFactory().Create();

peer.StateChanged.Subscribe(state => Console.WriteLine($"State: {state}"));
peer.Received.Subscribe(data => Console.WriteLine(Encoding.UTF8.GetString(data.Span)));

await peer.Start("ttySLG0");
```

Subscribing first means no state change and no early frame is missed. The data an observer receives is backed by an array allocated for that frame, so it can be kept or passed to another thread, but it is shared between observers and must not be modified.

## Wait for the remote peer instead of sending requests

```csharp
using BlueHeighliner.MicroGate;

MicroGatePeerOptions options = new() { RetryInterval = null };

await using IMicroGatePeer peer = new MicroGatePeerFactory().Create();

using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(30));
await peer.Start("ttySLG0", options, timeout.Token);
```

With a `null` retry interval this side never sends a connection request and completes when the remote peer's request arrives. By default both sides send requests every second until answered, so either may be started first.

## Change how often requests are sent

```csharp
using BlueHeighliner.MicroGate;

MicroGatePeerOptions options = new() { RetryInterval = TimeSpan.FromMilliseconds(250) };

await using IMicroGatePeer peer = new MicroGatePeerFactory().Create();
await peer.Start("ttySLG0", options);
```

## Tune retransmission

```csharp
using BlueHeighliner.MicroGate;

MicroGatePeerOptions options = new() { RetransmitInterval = TimeSpan.FromMilliseconds(250) };

await using IMicroGatePeer peer = new MicroGatePeerFactory().Create();
await peer.Start("ttySLG0", options);

byte[] message = new byte[peer.MaxPayloadSize];
await peer.Send(message);
```

Sent data is kept until the remote peer acknowledges it and is sent again if the remote peer rejects a gap or nothing is acknowledged within the interval. Use a `null` interval to resend only on rejection. A payload can be at most `MaxPayloadSize` bytes, and up to 7 sends can be unacknowledged before the next one waits.

## React to a disconnect

```csharp
peer.StateChanged.Subscribe(
    state => Console.WriteLine($"State: {state}"),
    () => Console.WriteLine("Peer is finished."));
```

`StateChanged` completes when the peer becomes `Disconnected`, whether the remote peer disconnected, the device was lost, or the peer was disposed.

## Always zero poll/final bit

```csharp
using BlueHeighliner.MicroGate;

MicroGatePeerOptions options = new() { Address = 0x01, DisablePollFinalBit = true };

await using IMicroGatePeer peer = new MicroGatePeerFactory().Create();
await peer.Start("ttySLG0", options);
```

With `DisablePollFinalBit`, every frame the station sends has the poll/final bit at 0, including acknowledgements to a peer frame that had it set.

## Configure the device

```csharp
using BlueHeighliner.MicroGate;

MicroGatePeerOptions options = new()
{
    Encoding = MicroGateEncoding.NrziSpace,
    Crc = MicroGateCrc.Crc32Ccitt,
    IdlePattern = MicroGateIdlePattern.Flags,
    HardwareAddressFilter = 0x01,
};

await using IMicroGatePeer peer = new MicroGatePeerFactory().Create();
await peer.Start("ttySLG0", options);
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
