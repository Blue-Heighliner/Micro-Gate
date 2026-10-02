# Project

Documentation for this repository's own tooling and workflow.

## Scripts

`Scripts/` holds file-based C# apps (`dotnet run Scripts/<Name>.cs`) for automated repository
actions.

- `Scripts/Test.cs` - runs the test suite with coverage collection and prints a summary.
- `Scripts/Verify.cs` - applies formatting fixes and regenerates the coverage badge.
- `Scripts/Publish.cs` - cuts a release (see Publishing below). A manual, human-only action.
- `Scripts/RunMonitor.cs` - runs one Controller from source in monitor mode.
- `Scripts/RunPeers.cs` - builds Controller once and runs two instances side by side in peer mode, addressed to each other (local 1 / remote 3 and local 3 / remote 1), for two peers on two cabled devices.
- `Scripts/RunPassthrough.cs` - builds Controller once and runs three instances: two peers addressed to each other and one passthrough, for two peers talking through a passthrough (which uses two devices) over four cabled devices.

## Publishing

Run `Scripts/Publish.cs` locally to cut a release:

1. It prompts for the version to publish (e.g. `1.2.3`). `Core/Core.csproj` carries no `<Version>` of its
   own, so this is what actually gets built and published.
2. It creates the GitHub Release (and its underlying tag) for that version locally via `gh release
   create`, done locally because a repo ruleset blocks the default `GITHUB_TOKEN` from creating tags.
3. It dispatches `build.yml`'s `workflow_dispatch` trigger with the version as input. The workflow
   verifies the dispatcher has Admin permission on the repo, refuses to run from anything but `main`,
   packs `Core/Core.csproj`, pushes the package to GitHub Packages and nuget.org, and uploads the
   `.nupkg`/`.snupkg` as release assets.
4. It waits for that run to finish, rolling the release/tag back if the workflow fails, so a failed
   publish never leaves one behind. If it can't confirm the run happened at all, it leaves the release
   in place instead, rather than risk deleting one that's still running.

## Workflows

- `.github/workflows/build.yml` - builds, verifies formatting (`dotnet format --verify-no-changes`,
  never applies fixes), and runs tests on every push/PR to `main`; its `publish` job (see Publishing
  above) only runs on `workflow_dispatch`, gated on the dispatcher having Admin permission on the repo
  and the run being on `main`, and pushes the package to both GitHub Packages and nuget.org.
- `.github/workflows/codeql.yml` - CodeQL security analysis on push/PR to `main` and a weekly schedule.

## Tests

`Tests/src/Unit/` mirrors `Core/src` (and, in `Controller/`, the Controller's log serializer and helpers, which it sees through `InternalsVisibleTo`) and uses Moq for every dependency; it needs no real I/O. `Tests/src/Integration/` uses real I/O and is organized by scenario:

- Two peers joined by a loopback TCP socket pair standing in for the cable (one or both sides sending requests, ordering, large payloads, disconnect).
- The Linux device opener and native layer against a real pseudo-terminal, with a peer implementing the HDLC state machine on the master side, plus regular files and missing devices for failure paths.
- Port enumeration against a temporary folder tree shaped like `/dev` and `/dev/serial/by-id`.
- A link that drops chosen frames, checking that everything still arrives in order through rejects and the retransmit timer.
- A relay: two peers started with monitoring, each forwarding the frames the other monitors, carry a connection between two endpoint peers, and report every frame.

Linux-only tests return early on other operating systems, and the pseudo-terminal tests return early if none can be created. The pseudo-terminal and file tests wrap the real native layer so that the SyncLink-specific configuration calls, which only a real device accepts, are skipped; a separate test checks that the real layer rejects a non-SyncLink device. The Windows native adapter can only be exercised on Windows with the driver installed, so it has no test.

## Controller

`Controller/` is an Avalonia desktop application, not part of the published package, with three modes chosen in the sidebar (see the mode combo box; `--mode Peer|Monitor|Passthrough` on the command line, case-insensitive, sets the mode it opens in, and `--local` and `--remote` (0-255) set the addresses; an unknown or invalid value is reported in the log and that option keeps its default; the sidebar shows only the settings that apply to the chosen mode). All three show frames and data the same way. The log has two views, switchable at any time in every mode without clearing it (the log keeps every row and the Log view setting only decides which are shown): Frames, the default, has one row per HDLC frame, and Data has one row per message. A frame row names its fields with a colon, such as `I  Address:45  Send:1  Receive:2  Poll/Final:0  Data:7B`; expanding it lists every field (`Address`, `Control`, `Frame Type`, `Poll/Final`, `Send`, `Receive`, `Data`) above a table that holds only the frame's data, so the address and control bytes never appear among the data, and a frame that could not be parsed shows its error and its raw bytes instead. Every field and value is a byte value from 0 to 255 and sizes are written like `7B`; hexadecimal is not shown anywhere, and the address settings are decimal. Clicking a row (or pressing Enter or Space on it) toggles it between expanded and collapsed, and expanding one collapses the previously expanded row; clicking inside an expanded row's field list or table does not toggle it. The Auto scroll switch, on by default, scrolls the log to the newest row whenever one is logged. Tables are 30 cells wide by default (the Display section's table columns setting changes it for every table, the send table included) with each cell's 0-based index above it. Cells show their ASCII character (control characters as abbreviations such as `LF`, values above 127 as numbers) and can be multi-selected (click, Ctrl, Shift, drag) and switched via the context menu between ASCII and the 0-255 value.

- **Peer** starts an `IMicroGatePeer` on one port and connects it with the two addresses. Its frame rows are the frames received (`Monitored`) and written (`Transmitted`), labeled Receive and Transmit, including the connection requests, acknowledgements, and polls; its data rows are the payloads received and sent. Received payloads are copied out of the pooled owner and disposed on the peer's delivery task, then posted to the UI thread. The send box is the same kind of table, editable: in ASCII input a typed character fills the selected cell and selects the next, in raw input a typed 0-255 value fills the cell and Tab (or a complete value) moves on; Enter sends and the table is emptied once the data is sent. Its context menu inserts or replaces the selection with a control character (NUL, SOH, STX, ETX, LF, and so on) and deletes cells, which shortens the frame; the grid holds at most the configured max info field. Payloads over `MaxPayloadSize` are rejected before sending.
- **Monitor** starts a peer with `EnableMonitor` on one port and never connects it, so nothing is sent. Every frame on the `Monitored` stream is a frame row, and every information frame that carries data is also a data row. There is no send box.
- **Passthrough** starts two peers with `EnableMonitor`, one per port, and forwards each peer's monitored frames to the other with `Forward`, through one unbounded channel and forwarding task per direction so a slow line never blocks the thread reading the other device. Frames and the data they carry are logged as rows labeled with the port names, such as `ttyUSB1 > ttyUSB2`. With two bricks cabled directly to each other, a relay between them feeds every frame straight back, so it needs a third station on one side to be useful.

The log can be saved to a JSON file and loaded back later, replacing what is on screen: each row keeps its text, its bytes (Base64), and which cells were switched to their 0-255 value. Loaded rows expand into the same tables.

The peer options live in the sidebar's Link Options section, collapsed by default, which shows only the settings that apply to the current mode. The settings that must match the remote station (encoding, CRC, clocking) apply in every mode; the transmit settings (idle pattern, preamble, underrun action) in peer and passthrough; the connection settings (info field, window, retry and retransmit intervals, poll/final, loopback) only in peer mode.

Controller is meant to be handed to someone who does not have the .NET SDK installed, so it is published as one self-contained executable rather than run from source:

```sh
dotnet publish Controller -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true
dotnet publish Controller -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

`Controller.csproj` lists `linux-x64` and `win-x64` in `RuntimeIdentifiers` so both restore ahead of time; `SelfContained`, `PublishSingleFile`, and `IncludeNativeLibrariesForSelfExtract` are conditioned on a `RuntimeIdentifier` actually being set, so a plain `dotnet build`/`dotnet run` during development, and the solution-wide CI build, are unaffected and stay ordinary framework-dependent builds. The output is one executable (`SerialController` on Linux, `SerialController.exe` on Windows) with the .NET runtime, Avalonia, and every dependency bundled inside it. Nothing else is published beside it: managed debug information is embedded in the executable (`Controller.csproj` sets `DebugType` to `embedded` and passes the same to `Core` through its project reference, only when a `RuntimeIdentifier` is set, so normal builds and the NuGet symbols package keep separate `.pdb` files), and the separate `.pdb` files the native libraries ship and the XML documentation file are removed from the publish list.
