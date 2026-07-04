# Strong WebSocket Signaling Contract Design

## Goal

Define and enforce one authenticated signaling envelope for Windows publishers, Flutter viewers, and the API while retaining raw ASP.NET Core WebSockets and the existing 64 KiB message limit.

## Considered approaches

1. Normalize every inbound and server-generated message into one envelope. This is the selected approach because clients consume one stable shape and the server remains authoritative for security-sensitive metadata.
2. Preserve the existing compact control frames and normalize only routed WebRTC frames. This minimizes changes but leaves clients with two contracts and does not meet the requested envelope consistency.
3. Introduce typed DTOs per message type. This provides compile-time payload schemas but is inappropriate because SDP and ICE payloads must remain opaque to the signaling server.

## Contract

Every outbound frame contains `type`, `messageId`, `sessionId`, `from`, `to`, `timestamp`, and `payload`. UUID values are JSON strings, `timestamp` is UTC ISO 8601, and absent participants are represented by `null`. The server generates missing message IDs for client frames and always generates IDs for server control frames.

Clients may send `ping` without a recipient. The server responds with `pong`. Routed client types are `publisher.ready`, `viewer.ready`, `webrtc.offer`, `webrtc.answer`, `webrtc.ice_candidate`, and `pong`; they require a UUID `to`. `session.joined`, `session.left`, `session.ended`, and `error` are server-generated only.

The server derives `sessionId` from the authenticated connection and overwrites `from` with its participant ID. It validates that `to` is a live connection in that same session. Client-supplied sender and session metadata are never trusted.

`payload` is copied as opaque JSON. SDP describes peer media/session parameters, while ICE candidates describe network paths discovered by peers. The signaling server routes these values but neither interprets nor logs them.

Errors use the same envelope with `type: "error"`, the current session and participant as server-known metadata, and `{ "code": "..." }` as payload. Invalid JSON, invalid envelopes, unsupported types, invalid recipients, and unavailable same-session recipients produce structured errors without closing an otherwise valid socket.

## Lifecycle and cleanup

Admission continues to require authentication, a valid active session, an owned eligible device, and a matching participant. Terminal sessions reject admission and prevent routing. Existing connections receive `session.ended` and close. Cleanup unregisters the connection and updates persistence, but cleanup failures must not hide the original socket termination.

## Logging

Successful routing logs only `messageType`, `sessionId`, `fromParticipantId`, `toParticipantId`, and `messageId`. SDP, ICE candidates, and all other payload content are excluded.

## Testing

Focused integration tests cover anonymous and invalid participant admission, cross-session isolation, sender overwrite, the complete normalized envelope, unsupported types, invalid JSON, terminal sessions, and payload-safe logging already exercised by the existing hardening coverage. Tests use actual WebSocket connections and database state rather than mocking routing behavior.

