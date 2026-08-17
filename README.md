# MicroGate

A C# API for using [MicroGate](https://www.microgate.com) SyncLink devices and drivers to create and communicate over serial USB/PCI card devices using the HDLC protocol in asynchronous balanced mode (ABM), on both Windows and Linux.

## Projects

| Project | Description |
| --- | --- |
| [`Core`](Core) | The `BlueHeighliner.MicroGate.Core` library, published as a NuGet package. Provides the API for enumerating SyncLink ports, opening connections to them, and exchanging HDLC/ABM frames over them, on Windows (via `mghdlc.dll`'s base API) and Linux (via the SyncLink driver's tty device). |
| [`Sample`](Sample) | An Avalonia desktop application demonstrating `Core`: enumerate ports, connect, and send/receive messages. |
| [`Tests`](Tests) | xUnit unit tests for `Core`. |

## Documentation

See [`Docs`](Docs) for architecture, component, and protocol documentation:

- [`Docs/Architecture.md`](Docs/Architecture.md) — how the projects and layers fit together, and why.
- [`Docs/Components.md`](Docs/Components.md) — what each type/file does.
- [`Docs/Protocols.md`](Docs/Protocols.md) — the HDLC/ABM wire protocol this library implements.

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- MicroGate SyncLink hardware and drivers, for running against real devices

## Building

```
dotnet build
```

## Testing

```
dotnet test
```

## Running the sample

```
dotnet run --project Sample
```
