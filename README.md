# MicroGate

[![NuGet](https://img.shields.io/nuget/v/BlueHeighliner.MicroGate.svg?label=NuGet)](https://www.nuget.org/packages/BlueHeighliner.MicroGate)
[![License: MIT](https://img.shields.io/github/license/Blue-Heighliner/Micro-Gate.svg)](LICENSE)
[![Build](https://github.com/Blue-Heighliner/Micro-Gate/actions/workflows/build.yml/badge.svg)](https://github.com/Blue-Heighliner/Micro-Gate/actions/workflows/build.yml)
[![Coverage](https://raw.githubusercontent.com/Blue-Heighliner/Micro-Gate/main/.github/badges/badge_linecoverage.svg)](https://github.com/Blue-Heighliner/Micro-Gate/actions/workflows/build.yml)

A C# API for using [MicroGate](https://www.microgate.com) SyncLink devices and drivers to create and communicate over serial USB/PCI card devices using the HDLC protocol in asynchronous balanced mode (ABM), compatible with any HDLC or ADCCP station in that mode, on both Windows (via `mghdlc.dll`'s base API) and Linux (via the SyncLink driver's tty device). Its only dependency is [System.Reactive](https://github.com/dotnet/reactive); see [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt).

## Installing

```sh
dotnet add package BlueHeighliner.MicroGate
```

## Getting started

```csharp
using BlueHeighliner.MicroGate;

IMicroGatePortSource ports = new MicroGatePortSource();
IMicroGatePeerFactory factory = new MicroGatePeerFactory();

IReadOnlyList<string> names = await ports.GetPorts();

await using IMicroGatePeer peer = factory.Create();
peer.Receiver = data =>
{
    using (data)
    {
        Console.WriteLine(System.Text.Encoding.UTF8.GetString(data.Memory.Span));
    }
};

await peer.Start(names[0]);
await peer.Connect(address: 0x01, remoteAddress: 0x03);
await peer.Send("Hello"u8.ToArray());
```

## Documentation

| File | Covers |
| --- | --- |
| [`Docs/Api.md`](Docs/Api.md) | The public API design and flow. |
| [`Docs/Usage.md`](Docs/Usage.md) | Runnable usage examples. |
| [`Docs/Architecture.md`](Docs/Architecture.md) | High-level design decisions. |
| [`Docs/Project.md`](Docs/Project.md) | This repository's scripts, publishing, and CI. |
| [`Docs/MicroGate/`](Docs/MicroGate) | The vendor serial API documentation and driver headers (`synclink.h`, `Mghdlc.h`) for Linux and Windows that the native layers are written against. |
| [`Docs/Components/`](Docs/Components) | One file per complex internal component (HDLC engine, peer lifecycle, Linux and Windows transports). |

## Controller

`Controller/` is an Avalonia desktop application with three modes. **Peer** opens one port and forms an HDLC connection: browse received data as expandable byte tables (ASCII or 0-255 values per cell) and compose data to send in the same kind of table, typing ASCII characters or raw byte values and inserting control characters. **Monitor** opens one port and only observes the frames received on it, forming no connection and sending nothing. **Passthrough** opens two ports and relays every frame between them as if it were not there, logging the frames in both directions. Run it with `dotnet run --project Controller` (add `-- --mode Monitor` or `-- --mode Passthrough` to open in another mode, and `--local 1 --remote 3` to set the addresses), or publish it as a single self-contained `SerialController` executable (see [`Docs/Project.md`](Docs/Project.md)).
