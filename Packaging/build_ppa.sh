command -v gpg > /dev/null 2>&1 || { echo "Error: gpg command is not available" >&2; exit 1; }

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

"$SCRIPT_DIR/build_dpkg.sh" -o "$SCRIPT_DIR/PPA" || { echo "Error: build_dpkg failed" >&2; exit 1; }

# Generate Packages file and compress it
dpkg-scanpackages --multiversion "$SCRIPT_DIR/PPA" > "$SCRIPT_DIR/PPA/Packages"
gzip -k -f "$SCRIPT_DIR/PPA/Packages"

# Generate Release file
apt-ftparchive release "$SCRIPT_DIR/PPA" > "$SCRIPT_DIR/PPA/Release"

gpg "$SCRIPT_DIR/PPA/ppa-private-key.asc.gpg"

export GNUPGHOME=$(mktemp -d)

gpg --import "$SCRIPT_DIR/PPA/ppa-private-key.asc"

rm -f "$SCRIPT_DIR/PPA/ppa-private-key.asc"

gpg --local-user "styris_packaging@fastmail.com" -abs -o - "$SCRIPT_DIR/PPA/Release" > "$SCRIPT_DIR/PPA/Release.gpg"
gpg --local-user "styris_packaging@fastmail.com" --clearsign -o - "$SCRIPT_DIR/PPA/Release" > "$SCRIPT_DIR/PPA/InRelease"

rm -rf "$GNUPGHOME"

echo "PPA build completed. Please commit and push changes to make release publicly available."
