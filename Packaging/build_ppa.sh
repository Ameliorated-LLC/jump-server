if [[ -z "$1" || ! "$1" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
    echo "Wrong version format"
    exit 1
fi

CURDIR="$(pwd)"

command -v gpg > /dev/null 2>&1 || { echo "Error: gpg command is not available" >&2; exit 1; }
command -v git > /dev/null 2>&1 || { echo "Error: git command is not available" >&2; exit 1; }

git filter-repo 2>&1 | grep -x "No arguments specified." > /dev/null || { echo -e "Error: git-filter-repo must be installed. Use one of the following commands to install git-filter-repo:\n\npip install git-filter-repo\nsudo apt install git-filter-repo\n" >&2; exit 1; }

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
GIT_DIR="$(cd "$SCRIPT_DIR" && cd .. && pwd)"

chmod +x "$SCRIPT_DIR/build_dpkg.sh"
"$SCRIPT_DIR/build_dpkg.sh" $1 -o "$SCRIPT_DIR/PPA/pool/main/j/jumper/jumper_$1-1_amd64.deb" || { echo "Error: build_dpkg failed" >&2; exit 1; }
# "$SCRIPT_DIR/build_dpkg.sh" $1 -o "$SCRIPT_DIR/PPA/pool/main/j/jumper/jumper_$1-1~noble-jammy-focal_amd64.deb" || { echo "Error: build_dpkg failed" >&2; exit 1; }

rm -rf "/tmp/JumpPPA"
cp -rf "$SCRIPT_DIR/PPA" "/tmp/JumpPPA"

cd /tmp/JumpPPA

gpg "ppa-private-key.asc.gpg"

export GNUPGHOME=$(mktemp -d)

gpg --import "ppa-private-key.asc"

rm -f "ppa-private-key.asc"

apt-ftparchive packages pool/ > "Packages"
gzip -k -f "Packages" > "Packages.gz"

# Generate Release, Release.gpg, and InRelease files
apt-ftparchive release "-c=aptftp.conf" . > "Release"
gpg --local-user "styris_packaging@fastmail.com" -abs -o - "Release" > "Release.gpg"
gpg --local-user "styris_packaging@fastmail.com" --clearsign -o - "Release" > "InRelease"

for DIST in "/tmp/JumpPPA/dists"/*; do
    if [ -d "$DIST" ]; then
        DIST_FOLDER=$(basename "$DIST")

        apt-ftparchive packages pool/ > "dists/$DIST_FOLDER/main/binary-amd64/Packages"
        gzip -k -f "dists/$DIST_FOLDER/main/binary-amd64/Packages" > "dists/$DIST_FOLDER/main/binary-amd64/Packages.gz"

        # Generate Release, Release.gpg, and InRelease files
        apt-ftparchive release "-c=dists/$DIST_FOLDER/aptftp.conf" "dists/$DIST_FOLDER" > "dists/$DIST_FOLDER/Release"
        gpg --local-user "styris_packaging@fastmail.com" -abs -o - "dists/$DIST_FOLDER/Release" > "dists/$DIST_FOLDER/Release.gpg"
        gpg --local-user "styris_packaging@fastmail.com" --clearsign -o - "dists/$DIST_FOLDER/Release" > "dists/$DIST_FOLDER/InRelease"
    fi
done

rm -rf "$GNUPGHOME"

cd "$GIT_DIR"
git commit -am "Temporary filter commit"
git filter-repo --path-regex "^Packaging/PPA/(?!.*\.gitkeep$).*$" --invert-paths --force
git reset HEAD~
git add "Packaging/PPA/*"

git remote add origin https://github.com/Ameliorated-LLC/jumper
git fetch origin
git branch --set-upstream-to=origin/main main

cp -rf /tmp/JumpPPA/* "$SCRIPT_DIR/PPA"
rm -rf "/tmp/JumpPPA"

cd "$CURDIR"

echo -e "\nPPA build completed. Please commit and FORCE push changes to make release publicly available."
