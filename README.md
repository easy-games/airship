# Airship

## Local Development
You can install this package locally to test in real time. 
1. Clone this project as a sibling to your unity project
2. Replace the airship entry in your `Packages/manifest.json` with this line:
```json
"gg.easy.airship": "file:../../airship",
```
3. Open your game's project in Rider (not this repo, your game!)


## Exiting Local Development
To get out of local development, change the line to this:
```json
"gg.easy.airship": "0.1.91"
```
