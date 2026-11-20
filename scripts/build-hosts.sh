#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/../server"
for os in linux windows; do
  name="carousel-$os-amd64"
  if [[ "$os" == windows ]]; then name+=".exe"; fi
  CGO_ENABLED=0 GOOS="$os" GOARCH=amd64 go build -trimpath -buildvcs=false -ldflags='-s -w' -o "../Assets/StreamingAssets/Host/$name" .
done
