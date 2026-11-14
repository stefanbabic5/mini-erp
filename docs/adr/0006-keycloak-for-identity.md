# ADR-0006: Keycloak as the identity provider

**Status:** Accepted · **Date:** 2026-11-14

## Context
The API needs authenticated users with a tenant and roles. Real systems delegate login to an identity provider over OpenID Connect (OIDC) and only validate tokens in the API. That is also what interviewers expect a medior backend developer to understand. Hosted providers (Auth0, Entra ID) have free tiers, but they depend on an outside account and network, and their terms change.

## Decision
- **Keycloak** (Apache 2.0) runs in Docker Compose with a realm imported at start-up: two tenants, a user per role in each, and a client for the API and one for the future UI.
- Users carry `tenant_id` as a user attribute, mapped into the access token. Roles map to a `roles` claim.
- The API validates JWT access tokens (`JwtBearer`: issuer, audience, signature from the JWKS endpoint, lifetime) and uses authorization policies per permission, not raw role checks in endpoints.
- Integration tests do not need Keycloak running. They sign tokens with a test key and configure the API to trust it.

## Alternatives considered
- **ASP.NET Core Identity with its built-in token endpoints**: simple, but the tokens are not standard JWTs, and it mixes user management into the API.
- **A hand-written JWT issuer**: fine for learning, but rolling your own auth is the classic thing not to do, and it teaches the wrong habit.
- **A hosted provider (Auth0, Entra ID free tier)**: realistic, but an outside dependency the project cannot run without, and terms can change.

## Consequences
- The API is provider-agnostic: switching to Entra ID or Auth0 is configuration (authority, audience, claim mapping).
- Keycloak is heavy (several hundred MB of memory) and slow to start, which matters for laptop resources and CI. CI therefore uses test-signed tokens.
- The realm export file must be kept in sync with the roles the API expects.
