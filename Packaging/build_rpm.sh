if [[ -z "$1" || ! "$1" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
    echo "Wrong version format"
    exit 1
fi

if ! command -v dotnet &> /dev/null
then
  export DOTNET_ROOT=$HOME/.dotnet
  DOTNET_ROOT=$HOME/.dotnet
  PATH=$PATH:$DOTNET_ROOT:$DOTNET_ROOT/tools
fi

hash -r

command -v gpg > /dev/null 2>&1 || { echo "Error: gpg command is not available" >&2; exit 1; }
command -v pinentry > /dev/null 2>&1 || { echo -e "Error: pinentry for GPG is not available. Use the following command to install it:\n\nsudo dnf install pinentry\n" >&2; exit 1; }
command -v rpmsign > /dev/null 2>&1 || { echo -e "Error: rpmsign command is not available. Use the following command to install it:\n\nsudo dnf install rpm-sign\n" >&2; exit 1; }
command -v rpmbuild > /dev/null 2>&1 || { echo -e "Error: rpmbuild command is not available. Use the following command to install it:\n\nsudo dnf install rpm-build\n" >&2; exit 1; }

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
GIT_DIR="$(cd "$SCRIPT_DIR" && cd .. && pwd)"
TEMP_DIR="/tmp/JumpCompile"

rsync -aq --delete --exclude '/src/Jumper/bin' --exclude '/src/Jumper/obj' --exclude '/src/Jumper/.idea' --exclude '/src/PostInstallScript/bin' --exclude '/src/PostInstallScript/obj' --exclude '/src/PostInstallScript/.idea' --exclude '/src/PostRemoveScript/bin' --exclude '/src/PostRemoveScript/obj' --exclude '/src/PostRemoveScript/.idea' "$GIT_DIR/" "$TEMP_DIR/"

mkdir -p "$TEMP_DIR/Packaging/RPM/jumper-$1/usr/bin"
mkdir -p "$TEMP_DIR/Packaging/RPM/jumper-$1/usr/share/man/man1"
mkdir -p "$TEMP_DIR/Packaging/RPM/jumper-$1/etc/jumper"

gzip -k "$TEMP_DIR/Packaging/manpage"
mv "$TEMP_DIR/Packaging/manpage.gz" "$TEMP_DIR/Packaging/RPM/jumper-$1/usr/share/man/man1/jumper.1.gz"

sed -i "s|<GlobalVersion>.*</GlobalVersion>|<GlobalVersion>$1</GlobalVersion>|" "$TEMP_DIR/src/Directory.Build.props"
if ! dotnet publish "$TEMP_DIR/src/Jumper/Jumper.csproj" -c Release -r linux-x64 --self-contained -o "$TEMP_DIR/Packaging/RPM/jumper-$1/usr/bin"; then
  echo "Error: dotnet publish failed. Cancelling operation."
  rm -rf "$TEMP_DIR"
  exit 1
fi
rm -f "$TEMP_DIR/Packaging/RPM/jumper-$1/usr/bin/jumper.dbg"
chmod +x "$TEMP_DIR/Packaging/RPM/jumper-$1/usr/bin/jumper"
if [[ "$2" == "-o" && "$3" != "" ]]; then
  output="$3"
else
  output="."
fi

tar --create --file "$TEMP_DIR/Packaging/RPM/SOURCES/jumper-$1.tar.gz" -C "$TEMP_DIR/Packaging/RPM" "jumper-$1"

sed -i "s/Version: .*/Version: $1/" "$TEMP_DIR/Packaging/RPM/SPECS/jumper.spec"
rpmbuild --define "_topdir $TEMP_DIR/Packaging/RPM" -bb "$TEMP_DIR/Packaging/RPM/SPECS/jumper.spec"

gpg --output "$TEMP_DIR/Packaging/ppa-private-key.asc" --decrypt "$TEMP_DIR/Packaging/ppa-private-key.asc.gpg" || { rm -rf "$TEMP_DIR"; echo "Error: Decryption failed." >&2; exit 1; }

export GNUPGHOME=$(mktemp -d)

gpg --import "$TEMP_DIR/Packaging/ppa-private-key.asc"

rm -f "$TEMP_DIR/Packaging/ppa-private-key.asc"

rpmsign --addsign --define "_gpg_name styris_packaging@fastmail.com" --define "_gpg_path $GNUPGHOME" "$TEMP_DIR/Packaging/RPM/RPMS/x86_64/jumper-$1-1.fc$(rpm -E %{fedora}).x86_64.rpm"

rm -rf "$GNUPGHOME"

cp "$TEMP_DIR/Packaging/RPM/RPMS/x86_64/jumper-$1-1.fc$(rpm -E %{fedora}).x86_64.rpm" $output

rm -rf "$TEMP_DIR"
