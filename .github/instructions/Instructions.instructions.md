---
description: Use for production-safe work in the SpeedRankeds / SchraderRankedSystem PUCK mod
---

This workspace is the real production PUCK mod SpeedRankeds / SchraderRankedSystem.

Important rules:
- Always inspect the real code before deciding
- Always identify the live execution path first
- Prefer minimal, localized, production-safe changes
- Do not rewrite whole systems unless explicitly requested
- Do not invent Unity or NGO APIs
- Do not move server-authoritative logic to the client
- Do not assume old or duplicated code is the live path until verified

Architecture rules:
- Server-authoritative system
- Separate server and client projects
- Preserve existing competitive behavior unless explicitly asked to change it
- Do not fix server-authoritative problems only in client UI

Critical semantics:
- serverMode is an authoritative per-instance concept
- public and competitive are the established production modes unless explicitly extended
- the authoritative source should be the real live per-server config path, not a shared backend config
- do not reinterpret isPublic if it serves server-browser visibility

For fixes, always work in this order:
1. Root cause / findings
2. Live execution path
3. Minimal safe plan
4. Code / change
5. Why it works
6. Validation

Validation commands:
- dotnet build .\SchraderRankedSystem.Server.csproj -c Release
- dotnet build .\SchraderRankedSystem.Client.csproj -c Release

Do not use SchraderRankedSystem.csproj unless explicitly requested.

When writing prompts for me, always include a recommended thinking level:
- MEDIUM for localized fixes
- HIGH for reverse engineering, decompiled code analysis, architecture audits, or cross-project changes