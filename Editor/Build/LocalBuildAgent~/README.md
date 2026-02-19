# Airship Local Shadow Build Agent (PoC)

This local daemon builds multi-platform bundles without switching the active target in the creator's Unity editor session.

## Flow

1. Unity editor calls `LocalShadowBuildAgentClient`.
2. Client starts `airship_local_build_agent.js` (if needed) and sends a build request.
3. Agent syncs source project -> per-platform shadow project.
4. Agent runs headless Unity per platform:
   - `-executeMethod AirshipBuildAgentEntry.BuildGameAssetBundlesFromCommandLine`
5. Agent copies built output back to source project `bundles/games/<gameId>_vLocalBuild/<platform>`.

## Manual daemon run

```bash
node airship_local_build_agent.js server --port 46321
```

Health check:

```bash
curl http://127.0.0.1:46321/v1/health
```

## Unity integration

`CreateAssetBundles.BuildPlatforms(...)` now tries the local shadow build agent first for multi-platform builds and automatically falls back to the old in-editor target-switching loop if the agent fails.

Disable agent path temporarily:

```bash
export AIRSHIP_DISABLE_LOCAL_BUILD_AGENT=1
```

## Requirements

- Node.js
- `rsync`
- Unity editor with required platform modules installed
