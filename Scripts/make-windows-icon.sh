#!/bin/bash
# Builds Windows/DiskManager/Assets/AppIcon.ico from Resources/AppIcon.png (macOS: needs sips and python3).
set -euo pipefail
cd "$(dirname "$0")/.."

work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT
for size in 16 24 32 48 64 256; do
    sips -z "$size" "$size" Resources/AppIcon.png --out "$work/$size.png" >/dev/null
done
mkdir -p Windows/DiskManager/Assets
python3 - "$work" Windows/DiskManager/Assets/AppIcon.ico <<'PY'
import struct, sys, os
work, out = sys.argv[1], sys.argv[2]
sizes = [16, 24, 32, 48, 64, 256]
images = [open(os.path.join(work, f"{s}.png"), "rb").read() for s in sizes]
header = struct.pack("<HHH", 0, 1, len(sizes))
offset = 6 + 16 * len(sizes)
entries = b""
for size, data in zip(sizes, images):
    dim = 0 if size == 256 else size  # 0 means 256 in the ICO format
    entries += struct.pack("<BBBBHHII", dim, dim, 0, 0, 1, 32, len(data), offset)
    offset += len(data)
with open(out, "wb") as f:
    f.write(header + entries + b"".join(images))
PY
echo "Wrote Windows/DiskManager/Assets/AppIcon.ico"
