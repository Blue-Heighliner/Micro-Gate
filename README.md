# MicroGate

[![NuGet](https://img.shields.io/nuget/v/BlueHeighliner.MicroGate.svg?label=NuGet)](https://www.nuget.org/packages/BlueHeighliner.MicroGate)
[![License: MIT](https://img.shields.io/github/license/Blue-Heighliner/Micro-Gate.svg)](LICENSE)
[![Build](https://github.com/Blue-Heighliner/Micro-Gate/actions/workflows/build.yml/badge.svg)](https://github.com/Blue-Heighliner/Micro-Gate/actions/workflows/build.yml)
[![Coverage](https://raw.githubusercontent.com/Blue-Heighliner/Micro-Gate/main/.github/badges/badge_linecoverage.svg)](https://github.com/Blue-Heighliner/Micro-Gate/actions/workflows/build.yml)

A C# API for using [MicroGate](https://www.microgate.com) SyncLink devices and drivers to create and communicate over serial USB/PCI card devices using the HDLC protocol in asynchronous balanced mode (ABM), on both Windows (via `mghdlc.dll`'s base API) and Linux (via the SyncLink driver's tty device). It has no third-party dependencies; see [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt).

## Installing

```sh
dotnet add package BlueHeighliner.MicroGate
```

## Getting started

```csharp
using BlueHeighliner.MicroGate;

IMicroGatePortSource ports = new MicroGatePortSource();
IMicroGateConnector connector = new MicroGateConnector();

IReadOnlyList<string> names = await ports.GetPorts();

await using IMicroGateConnection connection = await connector.Connect(names[0]);
connection.Received += (_, data) =>
{
    using (data)
    {
        Console.WriteLine(System.Text.Encoding.UTF8.GetString(data.Memory.Span));
    }
};

await connection.Send("Hello"u8.ToArray());
```

## Documentation

| File | Covers |
| --- | --- |
| [`Docs/Api.md`](Docs/Api.md) | The public API design and flow. |
| [`Docs/Usage.md`](Docs/Usage.md) | Runnable usage examples. |
| [`Docs/Architecture.md`](Docs/Architecture.md) | High-level design decisions. |
| [`Docs/Project.md`](Docs/Project.md) | This repository's scripts, publishing, and CI. |
| [`Docs/Components/`](Docs/Components) | One file per complex internal component (HDLC engine, connection lifecycle, Linux and Windows transports). |

## Sample

`Sample/` is an Avalonia desktop application demonstrating the library: enumerate ports, connect, and send and receive messages. Run it with `dotnet run --project Sample`.
