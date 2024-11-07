if ! command -v dotnet &> /dev/null
then
  export DOTNET_ROOT=$HOME/.dotnet
  DOTNET_ROOT=$HOME/.dotnet
  PATH=$PATH:$DOTNET_ROOT:$DOTNET_ROOT/tools
fi

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
GIT_DIR="$(cd "$SCRIPT_DIR" && cd .. && pwd)"
TEMP_DIR="/tmp/JumpCompile"

rsync -aq --delete --exclude '/src/Jumper/bin' --exclude '/src/Jumper/obj' --exclude '/src/Jumper/.idea' --exclude '/src/PostInstallScript/bin' --exclude '/src/PostInstallScript/obj' --exclude '/src/PostInstallScript/.idea' --exclude '/src/PostRemoveScript/bin' --exclude '/src/PostRemoveScript/obj' --exclude '/src/PostRemoveScript/.idea' "$GIT_DIR/" "$TEMP_DIR/"

if [ "$1" == "--debug" ]; then
  dotnet publish "$TEMP_DIR/src/Jumper/Jumper.csproj" -c Debug -r linux-x64 --self-contained \
    -p:PublishAot=false \
    -p:PublishSingleFile=true \
    -p:SelfContained=true \
    -o ./debug

  rm -rf "$TEMP_DIR"
else
  if ! dotnet publish "$TEMP_DIR/src/Jumper/Jumper.csproj" -c Release -r linux-x64 --self-contained -o "$TEMP_DIR/Packaging/DPKG/usr/bin"; then
    echo "Error: dotnet publish failed. Cancelling operation."
    rm -rf "$TEMP_DIR"
    exit 1
  fi

  dotnet publish "$TEMP_DIR/src/PostInstallScript" -c Release -r linux-x64 --self-contained -o "$TEMP_DIR/Packaging/DPKG/DEBIAN"
  rm "$TEMP_DIR/Packaging/DPKG/DEBIAN/postinst.dbg"
  dotnet publish "$TEMP_DIR/src/PostRemoveScript" -c Release -r linux-x64 --self-contained -o "$TEMP_DIR/Packaging/DPKG/DEBIAN"
  rm "$TEMP_DIR/Packaging/DPKG/DEBIAN/postrm.dbg"
  # cp -rf ./jumper "$TEMP_DIR/Packaging/DPKG/usr/bin"
  chmod -R 0755 "$TEMP_DIR/Packaging/DPKG/DEBIAN"
  chmod +x "$TEMP_DIR/Packaging/DPKG/DEBIAN/postinst"
  chmod +x "$TEMP_DIR/Packaging/DPKG/DEBIAN/postrm"
  chmod +x "$TEMP_DIR/Packaging/DPKG/usr/bin/jumper"
  if [[ "$1" == "-o" && "$2" != "" ]]; then
    output="$2"
  else
    output="."
  fi
  dpkg-deb --build --root-owner-group "$TEMP_DIR/Packaging/DPKG" "$output"

  rm -rf "$TEMP_DIR"
fi
