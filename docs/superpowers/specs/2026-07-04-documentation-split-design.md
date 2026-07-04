# SonicRelay Documentation Split Design

## Goal

Turn the oversized README into a concise project entry point and move detailed material into focused, authoritative documents without losing useful diagrams or describing planned behavior as implemented.

## Document boundaries

- `README.md`: suite table, quick start, current implementation status, links to detailed documentation, and a concise CI/CD summary.
- `docs/architecture.md`: system boundaries, component responsibilities, repository structure, domain model, media topology, and architecture Mermaid diagrams.
- `docs/protocol.md`: implemented authentication, device, session, health-check, and signaling contracts, including request/response behavior and WebSocket message routing.
- `docs/security.md`: implemented authentication and authorization controls, session-code protections, rate limits, signaling validation, secret handling, and explicitly identified production gaps.
- `docs/deployment-vps-ssh.md`: local prerequisites relevant to deployment, production topology, GitHub Actions flow, SSH deployment, environment variables, verification, and rollback guidance.
- `docs/adr/*.md`: concise records for decisions already represented by the implementation, rather than speculative choices.

## Source of truth

Endpoint and status claims will be derived from `Program.cs`, endpoint mapping files, supporting infrastructure, integration tests, deployment manifests, and the GitHub Actions workflow. Existing README claims are context only and will be corrected when they disagree with code.

Implemented and planned work will be clearly separated. An endpoint will only be documented as implemented when it is mapped by the running API. Protocol details will match accepted fields, validation rules, authorization policies, response status codes, and routed WebSocket message types.

## Diagram handling

Useful Mermaid diagrams will be retained and moved to the document where they are canonical. System topology, domain relationships, primary flow, multi-user isolation, and peer-connection topology belong in `architecture.md`. Diagrams will not be duplicated in the README unless required for entry-point comprehension.

## ADR scope

ADRs will cover stable decisions supported by current code and infrastructure:

1. The backend is a control plane, while WebRTC clients carry media directly or through TURN.
2. ASP.NET Core Identity bearer tokens provide client authentication.
3. PostgreSQL stores durable state while Redis stores redeemable, expiring session codes.
4. Signaling uses authenticated WebSockets with participant-scoped routing.

Each ADR will state context, decision, status, and consequences.

## Verification

Documentation links and endpoint tables will be checked against the implementation. The smallest repository-provided E2E or integration command covering the API will run unattended. No full unrelated test suite or dependency update is in scope.
