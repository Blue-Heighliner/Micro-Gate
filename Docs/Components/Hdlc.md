# HDLC engine

Covers `HdlcStateMachine`, `HdlcFrame`, `HdlcFrameKind`, `HdlcConnectionState`, `HdlcReceiveResult`, and `HdlcFrameException` (all internal, namespace `BlueHeighliner.MicroGate.Hdlc`), which read their address and poll/final settings from `MicroGatePeerOptions`. Together they turn raw frame bytes into frames and back and track the asynchronous balanced mode (ABM) connection. The engine has no I/O of its own: transports move raw bytes, and this engine decides what those bytes mean and what to send back. Background: https://en.wikipedia.org/wiki/High-Level_Data_Link_Control.

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

Both stations use the single address byte from `MicroGatePeerOptions.Address` (default `0xFF`) on every frame they send. `Receive` silently ignores (no state change, no response) any inbound frame whose address differs. This is a point-to-point simplification for a two-station serial link, not command/response addressing for multidrop networks.

## Disabling the poll/final bit

`MicroGatePeerOptions.DisablePollFinalBit` is applied at every point the state machine would set the bit: SABM and DISC requests, and the UA, RR, and REJ responses that would otherwise mirror the peer's poll bit back as final. When set, the P/F bit is 0 on every frame in both directions, including responses to a peer frame that had it set. Information frames never set it either way.

## Connection state machine

`HdlcConnectionState` moves `Disconnected`, `Connecting`, `Connected`, `Disconnecting`, `Disconnected`.

- **Connect**: `CreateConnect` sends SABM (poll set unless disabled) and moves to `Connecting`. UA while `Connecting` moves to `Connected` and resets both sequence numbers to 0. A SABM from the peer at any time also moves straight to `Connected` and is answered with UA, so both ends may call connect at the same time with no fixed initiator, matching two combined stations under ABM.
- **Disconnect**: `CreateDisconnect` sends DISC and moves to `Disconnecting`. UA while `Disconnecting` moves to `Disconnected`. A DISC from the peer at any time moves to `Disconnected` and is answered with UA. DM or FRMR at any time moves to `Disconnected` with no reply.
- **Data transfer**: `CreateInformation` (only while `Connected`, otherwise `InvalidOperationException`) builds an I-frame carrying the current N(S) and expected N(R), advances N(S) modulo 8, and keeps a copy of the payload until the frame is acknowledged. At most 7 frames may be outstanding, the largest window that stays unambiguous with modulo 8 numbering; an eighth throws. On receipt, an I-frame whose N(S) matches the expected receive sequence is accepted: the payload is delivered, the expected sequence advances, and RR is returned. An out-of-order N(S) delivers nothing and returns REJ, but only the first one of a gap: later out-of-order frames are dropped silently until the missing frame arrives, so a burst of frames after a loss produces one REJ and one resend, not many.
- **Acknowledgement and retransmission**: every RR, RNR, REJ, and I-frame carries the peer's N(R), the number of the next frame it expects, which acknowledges every outstanding frame before it. An N(R) that does not fall within the outstanding frames is ignored. REJ additionally flags that the frames still outstanding must be sent again; the caller then obtains them, oldest first, from `CreateRetransmission` at the moment it writes them, which is also what timer driven resends use. Resent frames are rebuilt with the current N(R). A link reset restarts both sequence numbers at zero but does not lose data: a SABM received while connected keeps the outstanding frames, renumbers them from zero, and flags a resend. The state machine counts the requests it has sent that are still unanswered, so a UA answering a request sent while already connected (the answer to a retry that crossed the first answer) resets this side too, matching the remote side, which resets on every SABM; a UA with no request outstanding is ignored. A SABM received while not connected discards stale outstanding frames and reports them as acknowledged so the caller can release its window. `DiscardLastInformation` takes back the newest frame and its sequence number, for a frame that could not be written.

`HdlcReceiveResult` reports the resulting state, an optional delivered payload (referencing the memory the frame was received into, so it is valid only until that memory is reused), an optional response frame the caller must transmit, the number of frames acknowledged, and whether the outstanding frames must be sent again. `HdlcFrame.Parse` likewise slices its input rather than copying it.
