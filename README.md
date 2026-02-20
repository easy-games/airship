# Airship

## Package Modes

Airship can be consumed in two modes:

1. Source mode (best for engine/package development)
```json
"gg.easy.airship": "file:../../airship"
```
2. Binary mode (best for game projects that do not edit Airship C#)
```json
"gg.easy.airship": "<published-version>"
```

## Workspace Scripts

Use the workspace tooling in `/Users/luke/easy-unity/tools/airship-binary-package`:

- Build local binary package:
```bash
./tools/airship-binary-package/build-binary-airship-package.sh \
  --compiled-project /Users/luke/easy-unity/airship-testbed \
  --output-package /Users/luke/easy-unity/airship-binary
```

- Apply default repo package modes:
```bash
./tools/airship-binary-package/switch-airship-dependency.sh apply-defaults
```

- Show current repo package modes:
```bash
./tools/airship-binary-package/switch-airship-dependency.sh show
```

See `/Users/luke/easy-unity/tools/airship-binary-package/README.md` for full usage.

When building binary packages, review `BINARY_SCRIPT_REFERENCE_WARNINGS.txt` in the output package and validate referenced assets in Unity.
