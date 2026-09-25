# MicroGate

[![NuGet](https://img.shields.io/nuget/v/BlueHeighliner.MicroGate.svg?label=NuGet)](https://www.nuget.org/packages/BlueHeighliner.MicroGate)
[![License: MIT](https://img.shields.io/github/license/Blue-Heighliner/Micro-Gate.svg)](LICENSE)
[![Build](https://github.com/Blue-Heighliner/Micro-Gate/actions/workflows/build.yml/badge.svg)](https://github.com/Blue-Heighliner/Micro-Gate/actions/workflows/build.yml)
[![Coverage](https://raw.githubusercontent.com/Blue-Heighliner/Micro-Gate/main/.github/badges/badge_linecoverage.svg)](https://github.com/Blue-Heighliner/Micro-Gate/actions/workflows/build.yml)

A C# API for using [MicroGate](https://www.microgate.com) SyncLink devices and drivers to create and communicate over serial USB/PCI card devices using the HDLC protocol in asynchronous balanced mode (ABM), on both Windows (via `mghdlc.dll`'s base API) and Linux (via the SyncLink driver's tty device). Its only dependency is [System.Reactive](https://github.com/dotnet/reactive); see [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt).

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
peer.Received.Subscribe(data => Console.WriteLine(System.Text.Encoding.UTF8.GetString(data.Span)));

await peer.Start(names[0]);
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

## Sample

`Sample/` is an Avalonia desktop application demonstrating the library: enumerate ports, connect, and send and receive messages. Run it with `dotnet run --project Sample`.
