# Protocols

This describes the wire protocol `Core` implements: HDLC framing in asynchronous balanced mode (ABM), and the base API each platform's SDK exposes it through. Background: https://en.wikipedia.org/wiki/High-Level_Data_Link_Control.

## Base API vs. link layer

MicroGate's SDKs expose two distinct layers, and `Core` only uses the lower one:

- **Base API** (`MgslOpen`/`MgslRead`/`MgslWrite`/`MgslSetParams`/... on Windows; `open`/`read`/`write`/`ioctl` on the Linux tty device). Selecting HDLC mode here only configures the **bit-level framing sublayer**: 0x7E flag detection for frame boundaries, zero-bit insertion/removal for transparency, and FCS/CRC generation/checking. The driver hands back — and accepts — exactly the bytes between the flags, unmodified. It does not know or enforce any address/control field structure; one read or write call is exactly one frame.
- **Link layer** (`MgslDl*`, Windows only, implemented by `mghdlc.dll`): a full ABM protocol engine — `SABM`/`UA` establishment, sequenced I-frames, S-frames, retries, timers — that builds and parses frames for the caller. Linux has no equivalent; the SyncLink Linux driver has no ABM/link-layer code at all (confirmed by inspection of `synclink_gt.c`/`synclink_usb.c` — no SABM/UA/DISC handling, no N(S)/N(R) sequencing, nothing touching the control byte).

Because the link layer only exists on Windows, `Core` never uses it (see `Core/src/Windows/Mghdlc.cs` — only base API functions are P/Invoked). Instead, `Core`'s own `HdlcStateMachine` (`Core/src/Hdlc/`) implements ABM in userspace, on top of the base API's raw framing, identically on both platforms. Each `write()`/`MgslWrite()` call is built as *address byte + control byte + payload*; each `read()`/`MgslRead()` call is parsed back the same way before the payload is delivered.

## Control field encoding

HDLC's basic (modulo-8, non-extended) control field format, per frame kind, is:

| Frame type | Bit 0 (LSB) | Bits 1–3 | Bit 4 | Bits 5–7 |
| --- | --- | --- | --- | --- |
| Information (I) | `0` | N(S) | P/F | N(R) |
| Supervisory (S) | `1` | `0` + SS (2 bits) | P/F | N(R) |
| Unnumbered (U) | `1` | `1` + 2 modifier bits | P/F | 3 modifier bits |

`HdlcFrame` (`Core/src/Hdlc/HdlcFrame.cs`) encodes/decodes this directly as bitmasks, using the same control-byte values as the Linux kernel's LAPB implementation (`net/lapb`) — a well-established, independently verifiable HDLC/ABM implementation — since they're standard across HDLC/SDLC/LAPB:

| `HdlcFrameKind` | Control byte (P/F = 0) |
| --- | --- |
| `SetAsynchronousBalancedMode` (SABM) | `0x2F` |
| `Disconnect` (DISC) | `0x43` |
| `UnnumberedAcknowledge` (UA) | `0x63` |
| `DisconnectedMode` (DM) | `0x0F` |
| `FrameReject` (FRMR) | `0x87` |
| `ReceiveReady` (RR) | `0x01` |
| `ReceiveNotReady` (RNR) | `0x05` |
| `Reject` (REJ) | `0x09` |
| `Information` (I) | `0x00`, plus N(S) and N(R) |

The poll/final (P/F) bit is always bit 4 (`0x10`), regardless of frame type, so it's applied uniformly by OR-ing it into whichever base value applies.

**Addressing.** Both stations use the single address byte configured in `HdlcStationOptions.Address` for every frame they send, and `HdlcStateMachine.Receive` silently ignores (no state change, no response) any inbound frame whose address doesn't match — a simple point-to-point simplification appropriate to a two-station serial link, rather than implementing distinct command/response addressing for multidrop networks.

**Disabling the poll/final bit.** `HdlcStationOptions.DisablePollFinalBit` is threaded through every point `HdlcStateMachine` would otherwise set the bit (`SABM`/`DISC` requests, and the `UA`/`RR`/`REJ` responses that would normally mirror the peer's poll bit back as final): when set, the control byte's P/F bit is unconditionally `0`, on every frame, in both directions. `Information` frames never set the bit regardless of the flag, since nothing in this implementation depends on it there.

## Connection state machine

`IHdlcStateMachine`/`HdlcStateMachine` (`Core/src/Hdlc/HdlcStateMachine.cs`) tracks `HdlcConnectionState`: `Disconnected` → `Connecting` → `Connected` → `Disconnecting` → `Disconnected`.

- **Connect**: `CreateConnect()` sends `SABM` (poll bit set, unless disabled) and moves to `Connecting`. Receiving `UA` while `Connecting` moves to `Connected` (send/receive sequence numbers reset to 0). Receiving `SABM` from the peer at any time also moves straight to `Connected` and replies `UA` — this lets both ends call `Connect()` independently, at the same time, without a defined initiator/responder role, matching the two ends of a MicroGate SyncLink cable both being "combined stations" under ABM.
- **Disconnect**: `CreateDisconnect()` sends `DISC` and moves to `Disconnecting`. Receiving `UA` while `Disconnecting` moves to `Disconnected`. Receiving `DISC` from the peer at any time moves to `Disconnected` and replies `UA`. Receiving `DisconnectedMode` (DM) or `FrameReject` (FRMR) at any time also moves to `Disconnected` (no reply).
- **Data transfer**: `CreateInformation(payload)` (only valid while `Connected`, else throws `InvalidOperationException`) builds an I-frame carrying the current send sequence N(S) and the current expected-receive sequence N(R), then advances N(S). On receipt, an I-frame whose N(S) matches the locally expected receive sequence is accepted — payload delivered, expected sequence advanced, `RR` sent back acknowledging it; an out-of-order N(S) is rejected — no payload delivered, `REJ` sent back instead, requesting retransmission from the acknowledged point. `RR`/`RNR`/`REJ` received *from* the peer (acknowledging frames this station sent) are currently observed only for connection-state purposes; there is no retransmission/windowing buffer — reasonable for a directly-cabled serial link where the base API's own CRC checking already discards corrupted frames, but worth knowing if extending this for a lossier or shared-media link.

## Platform base API configuration

Both `ConfigurePort` methods (`LinuxMicroGateConnection`/`WindowsMicroGateConnection`) apply the same physical-layer defaults, expressed through each platform's native parameter struct:

- **Mode**: HDLC (`MGSL_MODE_HDLC`).
- **Encoding**: NRZ.
- **CRC**: CRC-16-CCITT, with corrupted frames silently discarded by the driver (frames failing CRC are never delivered to `read`/`MgslRead`).
- **Clock**: external, via the TXC/RXC pins (`Flags = 0`) — i.e. the DCE/cable supplies the clock, as is typical when driving a real device rather than an internal loopback/test rig.
- **Hardware address filter**: disabled (`0xFF`) — address filtering is handled by `HdlcStateMachine` instead, at the frame level, not by the hardware.

Linux additionally selects the `N_HDLC` tty line discipline (`TIOCSETD`) before applying `MGSL_IOCSPARAMS`, and clears `O_NONBLOCK` after opening (`open()` uses `O_NONBLOCK` only so it doesn't block on DCD) so subsequent `read`/`write` calls are blocking, matching the vendor SDK's own Linux sample (`c/samples/hdlc.c`).

## Frame size and buffering

Both platforms read into a 65535-byte buffer per receive call (`HDLC_MAX_FRAME_SIZE`, the protocol's maximum HDLC frame size) and deliver each accepted I-frame's payload to `Received` via a pooled `IMemoryOwner<byte>` (`MemoryPool<byte>.Shared`, trimmed to the actual payload length by `LimitedMemoryOwner`) rather than a fixed-size or newly-allocated array, so callers that process many small frames don't force a GC allocation per frame.
