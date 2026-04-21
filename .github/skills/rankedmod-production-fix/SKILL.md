---
name: rankedmod-production-fix
description: Production-safe audit and implementation behavior for the SpeedRankeds PUCK mod.
---

# RankedMod production fix

Use this skill when working on:
- SchraderRankedSystem.Server
- SchraderRankedSystem.Client
- server-authoritative ranked flow
- onboarding / verification
- serverMode behavior
- draft / post-match / overlays
- decompiled PUCK or reference mod audits

## Required behavior

Always inspect the real code before deciding.
Always identify the live execution path first.
Do not assume old or duplicated code is the live path.
Prefer minimal, localized, production-safe changes.
Do not rewrite architecture unless explicitly requested.
Do not move authority to the client.

## Critical project rules

- Preserve competitive behavior unless explicitly asked otherwise
- Do not let public contaminate official competitive behavior
- Do not reinterpret isPublic if it serves browser visibility
- Treat serverMode as an authoritative runtime concern
- Do not fix server-authoritative problems only in the client UI
- When dealing with training or decompiled mods, separate feature reference from architecture reuse

## Response structure

Always respond in this order:
1. Root cause / findings
2. Live execution path
3. Minimal safe plan
4. Code / change
5. Why it works
6. Validation

## Validation

For mod changes, validate with:
- dotnet build .\SchraderRankedSystem.Server.csproj -c Release
- dotnet build .\SchraderRankedSystem.Client.csproj -c Release

Do not use SchraderRankedSystem.csproj unless explicitly requested.

## Thinking recommendation

Recommend:
- MEDIUM for localized fixes
- HIGH for decompiled code, reverse engineering, cross-server/client flow audits, or new mode introduction