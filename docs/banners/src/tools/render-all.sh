#!/bin/bash
# Renders every composition: 1200x630 at scale 1 and 2, and the heroes at 1920x1080. Run from anywhere; it works in the
# banner folder (the one holding src/ and finals/). NODE_PATH must reach playwright.
cd "$(dirname "$0")/../.."
names="${@:-showcase-desk showcase-phones showcase-daynight focus-truss focus-utilization focus-failure focus-palette stylized-split stylized-stack stylized-mosaic focus-truss-titled focus-truss-hero stylized-split-hero}"
args=()
for n in $names; do
  if [[ $n == *-hero ]]; then args+=(src/$n.html finals/$n.png 1920 1080 1)
  else args+=(src/$n.html finals/$n@1x.png 1200 630 1 src/$n.html finals/$n@2x.png 1200 630 2); fi
done
node src/tools/render.js "${args[@]}"
