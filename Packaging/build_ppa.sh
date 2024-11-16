if [[ -z "$1" || ! "$1" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
    echo "Wrong version format"
    exit 1
fi

CURDIR="$(pwd)"

command -v gpg > /dev/null 2>&1 || { echo "Error: gpg command is not available" >&2; exit 1; }

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

"$SCRIPT_DIR/build_dpkg.sh" $1 -o "$SCRIPT_DIR/PPA/pool/main/j/jumper/jumper_$1-1~noble-jammy-focal_amd64.deb" || { echo "Error: build_dpkg failed" >&2; exit 1; }

rm -rf "/tmp/JumpPPA"
cp -rf "$SCRIPT_DIR/PPA" "/tmp/JumpPPA"

cd /tmp/JumpPPA

gpg "ppa-private-key.asc.gpg"

export GNUPGHOME=$(mktemp -d)

gpg --import "ppa-private-key.asc"

rm -f "ppa-private-key.asc"

for DIST in "/tmp/JumpPPA"/*; do
    if [ -d "$DIST" ]; then
        # Generate Packages files and compress them
        for ARCH_DIR in */binary-*; do
            [ -d "$ARCH_DIR" ] && dpkg-scanpackages --multiversion ../../../pool/main > "$ARCH_DIR/Packages" && gzip -k -f "$ARCH_DIR/Packages"
        done

        # Generate Release, Release.gpg, and InRelease files
        apt-ftparchive release . > Release
        gpg --local-user "styris_packaging@fastmail.com" -abs -o - Release > Release.gpg
        gpg --local-user "styris_packaging@fastmail.com" --clearsign -o - Release > InRelease
    fi
done

rm -rf "$GNUPGHOME"

cp -rf /tmp/JumpPPA/* "$SCRIPT_DIR/PPA"
rm -rf "/tmp/JumpPPA"

cd "$CURDIR"

echo "PPA build completed. Please commit and push changes to make release publicly available."
