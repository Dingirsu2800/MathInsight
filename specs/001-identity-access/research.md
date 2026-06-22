# Research: Identity & Access Management

This document outlines the technical research and design decisions made for the security implementations, token validation, and third-party integrations.

## Decision 1: Password Hashing Algorithm
- **Decision**: Use BCrypt (Work Factor 12) for hashing user passwords.
- **Rationale**: BCrypt is a standard, highly secure adaptive hashing algorithm that is resistant to brute-force and hardware-accelerated attacks (e.g. GPUs). Work factor 12 provides a solid balance of safety (~200ms processing delay per hash) and server CPU load.
- **Alternatives Considered**: 
  - Argon2id: While theoretically more secure, BCrypt is natively supported and simpler to set up in standard C# security packages without external native DLL wrappers.
  - SHA256: Rejected as it is too fast, making it highly vulnerable to dictionary/rainbow-table brute-force attacks.

## Decision 2: Google OAuth 2.0 Integration Flow
- **Decision**: Standard Authorization Code Flow with PKCE. The React frontend obtains the authorization code from Google, sends it to the backend API (`POST /api/v1/auth/google`), and the backend exchanges it for a Google Profile, registers the user if new, and issues a local JWT token.
- **Rationale**: Keeps Google Client Secrets secure on the server side. Enforces that all registrations (even social ones) pass through our database constraints.
- **Alternatives Considered**: 
  - Implicit Flow: Deprecated by OAuth WG due to token leakage risks in URL logs.
  - Frontend-only exchange: Rejected because it bypasses backend control, requiring the frontend to carry the backend's token generation keys.

## Decision 3: Single Student Session Concurrency (Blacklisting)
- **Decision**: Blacklist old JWT tokens in Redis. When a student logs in, the backend checks for any active sessions in Redis. If found, it adds the old token signature to a Redis blacklist with an expiration matching the token's TTL, and notifies the client via SignalR or immediate HTTP 401 on next request.
- **Rationale**: Fast, distributed, and does not require writing to SQL Server on every API request. Satisfies BR-02 (UC-07).
- **Alternatives Considered**: 
  - SQL Server database sessions check: Rejected because query overhead on every authenticated route slows page response time significantly.
