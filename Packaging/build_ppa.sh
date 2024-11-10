if [[ -z "$1" || ! "$1" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
    echo "Wrong version format"
    exit 1
fi

command -v gpg > /dev/null 2>&1 || { echo "Error: gpg command is not available" >&2; exit 1; }

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

"$SCRIPT_DIR/build_dpkg.sh" $1 -o "$SCRIPT_DIR/PPA" || { echo "Error: build_dpkg failed" >&2; exit 1; }

rm -rf "/tmp/JumpPPA"
cp -rf "$SCRIPT_DIR/PPA" "/tmp/JumpPPA"

# Generate Packages file and compress it
dpkg-scanpackages --multiversion "/tmp/JumpPPA" > "/tmp/JumpPPA/Packages"
gzip -k -f "/tmp/JumpPPA/Packages"

# Generate Release file
apt-ftparchive release "/tmp/JumpPPA" > "/tmp/JumpPPA/Release"

gpg "/tmp/JumpPPA/ppa-private-key.asc.gpg"

export GNUPGHOME=$(mktemp -d)

gpg --import "/tmp/JumpPPA/ppa-private-key.asc"

rm -f "/tmp/JumpPPA/ppa-private-key.asc"

gpg --local-user "styris_packaging@fastmail.com" -abs -o - "/tmp/JumpPPA/Release" > "/tmp/JumpPPA/Release.gpg"
gpg --local-user "styris_packaging@fastmail.com" --clearsign -o - "/tmp/JumpPPA/Release" > "/tmp/JumpPPA/InRelease"

rm -rf "$GNUPGHOME"

cp -rf /tmp/JumpPPA/* "$SCRIPT_DIR/PPA"
rm -rf "/tmp/JumpPPA"

echo "PPA build completed. Please commit and push changes to make release publicly available."
