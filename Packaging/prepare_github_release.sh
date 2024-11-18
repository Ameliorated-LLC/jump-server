CURDIR="$(pwd)"

command -v git > /dev/null 2>&1 || { echo "Error: git command is not available" >&2; exit 1; }

git filter-repo 2>&1 | grep -x "No arguments specified." > /dev/null || { echo -e "Error: git-filter-repo must be installed. Use one of the following commands to install git-filter-repo:\n\npip install git-filter-repo\nsudo apt install git-filter-repo\n" >&2; exit 1; }

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
GIT_DIR="$(cd "$SCRIPT_DIR" && cd .. && pwd)"

rm -rf "/tmp/JumpPPA"
cp -rf "$SCRIPT_DIR/PPA" "/tmp/JumpPPA"

cd "$GIT_DIR"
CACHED=true
git commit -am "Temporary filter commit" || CACHED=false
git filter-repo --path-regex "^Packaging/PPA/(?!.*\.gitkeep$).*$" --invert-paths --force
if [ "$CACHED" = true ]; then
  git reset HEAD~
fi

git remote add origin https://github.com/Ameliorated-LLC/jumper
git fetch origin
git branch --set-upstream-to=origin/main main

cp -rf /tmp/JumpPPA/* "$SCRIPT_DIR/PPA"
rm -rf "/tmp/JumpPPA"

cd "$CURDIR"

echo -e "\nRepository is ready for GitHub release. Please FORCE push changes before making a release."
echo -e "Once your release is published, run git add \"Packaging/PPA/*\" and commit + FORCE push."
"
