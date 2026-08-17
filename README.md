# MicroGate

A C# API for using [MicroGate](https://www.microgate.com) SyncLink devices and drivers to create and communicate over serial USB/PCI card devices using the HDLC protocol in asynchronous balanced mode (ABM).

## Projects

| Project | Description |
| --- | --- |
| [`Core`](Core) | The `BlueHeighliner.MicroGate.Core` library, published as a NuGet package. Provides the API for opening SyncLink devices and exchanging HDLC/ABM frames over them. |
| [`Sample`](Sample) | An Avalonia desktop application demonstrating `Core`. |
| [`Tests`](Tests) | xUnit unit tests for `Core`. |

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
