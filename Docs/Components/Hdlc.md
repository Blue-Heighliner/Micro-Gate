# HDLC engine

Covers `HdlcStateMachine`, `HdlcFrame`, `HdlcFrameKind`, `HdlcConnectionState`, `HdlcReceiveResult`, and `HdlcFrameException` (all internal, namespace `BlueHeighliner.MicroGate.Hdlc`), which read their address and poll/final settings from `MicroGateConnectionOptions`. Together they turn raw frame bytes into frames and back and track the asynchronous balanced mode (ABM) connection. The engine has no I/O of its own: transports move raw bytes, and this engine decides what those bytes mean and what to send back. Background: https://en.wikipedia.org/wiki/High-Level_Data_Link_Control.

## Base API versus link layer

The platform SDKs' base API only does bit-level framing: 0x7E flag detection, zero-bit insertion for transparency, and CRC generation and checking. The driver returns and accepts exactly the bytes between the flags; one read or write is exactly one frame, and no address or control structure is enforced. Each frame written is built as address byte, control byte, then payload, and each frame read is parsed back the same way.

## Control field encoding

The basic (modulo 8, non-extended) control field:

| Frame type | Bit 0 (LSB) | Bits 1 to 3 | Bit 4 | Bits 5 to 7 |
| --- | --- | --- | --- | --- |
| Information (I) | `0` | N(S) | P/F | N(R) |
| Supervisory (S) | `1` | `0` + SS (2 bits) | P/F | N(R) |
| Unnumbered (U) | `1` | `1` + 2 modifier bits | P/F | 3 modifier bits |

`HdlcFrame` encodes and decodes this with bitmasks, using the control byte values of the Linux kernel's LAPB implementation, which are standard across HDLC, SDLC, and LAPB:

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

The poll/final (P/F) bit is always bit 4 (`0x10`) regardless of frame type, so it is applied uniformly by OR-ing it into whichever base value applies. `HdlcFrame.Parse` throws `HdlcFrameException` for frames shorter than an address and control field or with a control byte matching no known kind; transports drop such frames and keep reading.

## Addressing

Both stations use the single address byte from `MicroGateConnectionOptions.Address` (default `0xFF`) on every frame they send. `Receive` silently ignores (no state change, no response) any inbound frame whose address differs. This is a point-to-point simplification for a two-station serial link, not command/response addressing for multidrop networks.

## Disabling the poll/final bit

`MicroGateConnectionOptions.DisablePollFinalBit` is applied at every point the state machine would set the bit: SABM and DISC requests, and the UA, RR, and REJ responses that would otherwise mirror the peer's poll bit back as final. When set, the P/F bit is 0 on every frame in both directions, including responses to a peer frame that had it set. Information frames never set it either way.

## Connection state machine

`HdlcConnectionState` moves `Disconnected`, `Connecting`, `Connected`, `Disconnecting`, `Disconnected`.

- **Connect**: `CreateConnect` sends SABM (poll set unless disabled) and moves to `Connecting`. UA while `Connecting` moves to `Connected` and resets both sequence numbers to 0. A SABM from the peer at any time also moves straight to `Connected` and is answered with UA, so both ends may call connect at the same time with no fixed initiator, matching two combined stations under ABM.
- **Disconnect**: `CreateDisconnect` sends DISC and moves to `Disconnecting`. UA while `Disconnecting` moves to `Disconnected`. A DISC from the peer at any time moves to `Disconnected` and is answered with UA. DM or FRMR at any time moves to `Disconnected` with no reply.
- **Data transfer**: `CreateInformation` (only while `Connected`, otherwise `InvalidOperationException`) builds an I-frame carrying the current N(S) and expected N(R), then advances N(S) modulo 8. On receipt, an I-frame whose N(S) matches the expected receive sequence is accepted: the payload is delivered, the expected sequence advances, and RR is returned. An out-of-order N(S) delivers nothing and returns REJ. RR, RNR, and REJ from the peer only matter for connection state.

`HdlcReceiveResult` reports the resulting state, an optional delivered payload, and an optional response frame the caller must transmit.
