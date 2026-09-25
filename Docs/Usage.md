# Usage

Runnable examples of `MicroGatePortSource`, `MicroGateConnector`, and `IMicroGateConnection` in different situations.

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

IMicroGateConnector connector = new MicroGateConnector();

await using IMicroGateConnection connection = await connector.Connect("ttySLG0");
await connection.Send("Hello"u8.ToArray());
```

With no options the default settings apply: NRZ encoding, CRC-16-CCITT, flag idle, and HDLC address `0xFF`, which the peer must also use.

## Receive data

```csharp
using System.Text;

connection.Received += (_, data) =>
{
    using (data)
    {
        Console.WriteLine(Encoding.UTF8.GetString(data.Memory.Span));
    }
};
```

The handler owns `data` and must dispose it; the `using` returns the pooled buffer.

## React to a disconnect

```csharp
connection.Disconnected += (_, _) => Console.WriteLine("Peer disconnected.");
```

## Always zero poll/final bit

```csharp
using BlueHeighliner.MicroGate;

MicroGateConnectionOptions options = new() { Address = 0x01, DisablePollFinalBit = true };

IMicroGateConnector connector = new MicroGateConnector();

await using IMicroGateConnection connection = await connector.Connect("ttySLG0", options);
```

With `DisablePollFinalBit`, every frame the station sends has the poll/final bit at 0, including acknowledgements to a peer frame that had it set.

## Configure the device

```csharp
using BlueHeighliner.MicroGate;

MicroGateConnectionOptions options = new()
{
    Encoding = MicroGateEncoding.NrziSpace,
    Crc = MicroGateCrc.Crc32Ccitt,
    IdlePattern = MicroGateIdlePattern.Flags,
    HardwareAddressFilter = 0x01,
};

IMicroGateConnector connector = new MicroGateConnector();

await using IMicroGateConnection connection = await connector.Connect("ttySLG0", options);
```

Both peers must agree on encoding, CRC, and idle pattern.

## Register with a container

```csharp
using BlueHeighliner.MicroGate;
using Microsoft.Extensions.DependencyInjection;

ServiceCollection services = new();
services.AddSingleton<IMicroGatePortSource, MicroGatePortSource>();
services.AddSingleton<IMicroGateConnector, MicroGateConnector>();
```
