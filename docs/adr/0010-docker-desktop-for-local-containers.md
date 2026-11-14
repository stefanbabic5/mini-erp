# ADR-0010: Docker Desktop for local containers

**Status:** Proposed · **Date:** 2026-11-14

## Context
The project runs PostgreSQL, Keycloak and the observability dashboard in containers ([ADR-0002](0002-postgresql-and-ef-core.md), [ADR-0006](0006-keycloak-for-identity.md)), and its integration tests start PostgreSQL through Testcontainers. Development happens on Windows 11, which needs a Linux container engine. [ADR-0007](0007-free-dependencies-only.md) allows "free for individuals" licences only as a recorded exception when no reasonable OSI-licensed option exists.

Docker Desktop is free for personal use, education, non-commercial open source and small businesses, but it is not open source. The container engine inside it (Moby) and the Docker CLI are Apache 2.0.

## Decision
- **Docker Desktop with the WSL 2 backend** (per-user installation, Linux containers) is the local container runtime on Windows. This is an exception to ADR-0007, recorded here.
- The project depends only on the **open parts**: the Docker CLI, the Docker Engine API and the Compose file format. Nothing in the repository requires a Docker Desktop feature, so any engine with a compatible API can replace it.
- CI runs on GitHub-hosted Linux runners, which ship Docker Engine (open source), so CI does not use Docker Desktop at all.

## Alternatives considered
- **Docker Engine installed directly in a WSL 2 distribution**: fully open source and within ADR-0007, but it needs manual setup (a Linux distribution, the engine, starting the daemon, exposing it to Windows tools). It remains the fallback if Docker Desktop's terms change.
- **Podman Desktop** (Apache 2.0): open source, but Testcontainers and Compose need extra configuration and have more rough edges on Windows.
- **Rancher Desktop** (Apache 2.0): open source and capable, but less common, and its default settings differ from what most .NET documentation assumes.

## Consequences

**What gets easier**
- One installer, and Testcontainers and `docker compose` work with no extra configuration.

**What gets harder**
- The project relies on a licence it does not control. If the terms change, the fallback is Docker Engine in WSL 2, with no change to the repository.

**Worth knowing**
- Business Central containers are Windows containers. Docker Desktop runs either Linux or Windows containers at a time, switched from the tray menu; this project needs Linux containers.
