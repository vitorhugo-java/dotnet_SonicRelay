# Strong WebSocket Signaling Contract Implementation Plan

**Goal:** Enforce and document one authoritative WebSocket signaling envelope.

**Architecture:** Keep the current endpoint and in-memory connection registry. Add focused envelope serialization helpers in the endpoint, normalize all server output, validate client metadata and recipients at the routing boundary, and preserve payloads as cloned opaque JSON.

**Tech stack:** .NET 10, ASP.NET Core raw WebSockets, EF Core, xUnit integration tests.

### Task 1: Lock the contract with integration tests

**Files:**
- Modify: `tests/SonicRelay.Api.IntegrationTests/SignalingWebSocketTests.cs`

Add tests for invalid participant admission, sender overwrite, normalized metadata/payload preservation, unsupported types, and malformed JSON. Run only `SignalingWebSocketTests` and confirm the new assertions fail for missing envelope fields or current error shapes.

### Task 2: Normalize and validate signaling frames

**Files:**
- Modify: `services/SonicRelay.Api/Endpoints/SignalingWebSocketEndpoint.cs`

Create one envelope serializer used by joined/left/ended/error/ping and routed messages. Preserve client `messageId` when valid, otherwise generate one. Derive session and sender fields server-side, require same-session recipients through the registry, keep WebRTC payload opaque, and log metadata only.

Run the focused signaling tests until all pass.

### Task 3: Document the public protocol

**Files:**
- Modify: `docs/protocol.md`

Replace compact control-frame examples with the canonical envelope, enumerate client and server message ownership, error payloads, validation rules, and the SDP/ICE logging restriction.

### Task 4: Verify and publish

Run the focused signaling integration tests and a project build. Inspect the exact diff, stage only the five scoped files, commit on `main`, and push `main` to `origin` as explicitly requested.

