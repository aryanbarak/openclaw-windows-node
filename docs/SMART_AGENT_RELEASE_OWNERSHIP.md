# Smart-Agent release ownership contract

This document separates current implementation from approved release architecture. It does not authorize repository creation, publishing, signing, or distribution.

## Current implementation

- Smart-Agent runtime update checks/downloads/install are disabled fail-closed.
- No Smart-Agent GitHub release repository has been created or published by this workspace.
- No production Smart-Agent code-signing identity is configured.
- No Smart-Agent release checksum/signature verification implementation is enabled.
- The inherited OpenClaw release workflow remains source-preserved and explicitly restricted to `openclaw/openclaw-windows-node`.
- Inno installer product/support/update URLs are omitted while release ownership is incomplete.
- Product GitHub/support links are hidden while repository ownership is incomplete.

## Approved release architecture

- Planned repository: `aryanbarak/smart-agent-windows`.
- Release tags retain upstream-compatible `v<semver>` version semantics unless a later architecture decision changes them.
- Inno installer names are `SmartAgentCompanion-Setup-x64.exe` and `SmartAgentCompanion-Setup-arm64.exe`.
- Smart-Agent releases must be published only from a Smart-Agent-owned repository. There is no fallback to OpenClaw release assets.
- Public release artifacts require a production Authenticode signing identity owned for Smart-Agent.
- Public release artifacts require a SHA-256 checksum manifest generated from the immutable release payload.
- The updater must verify its Smart-Agent-owned release source and the approved artifact integrity/signing policy before installation.
- Repository ownership, signing policy, verification policy, and runtime updater enablement are server/build-owned policy, not user-editable runtime settings.

## Planned activation sequence

1. Create/approve the Smart-Agent repository and release permissions.
2. Configure the production signing identity and protected release environment.
3. Implement and validate SHA-256 manifest generation and artifact verification.
4. Add a separate Smart-Agent release workflow and Smart-Agent artifact names; do not repurpose the upstream OpenClaw publication lane.
5. Enable product/support/update links only after the repository is live.
6. Enable the runtime updater only after all ownership, signing, and verification gates are true.
7. Perform side-by-side installer and updater smoke tests before any public distribution.

## Explicitly deferred

- Production signing certificate/provider selection.
- Microsoft Store/Partner Center production identity.
- Production release creation or upload.
- Automatic updater activation.
- Public support/update URLs before the planned repository actually exists.
