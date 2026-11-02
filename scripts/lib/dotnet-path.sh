# Sourced by the scripts that run dotnet. Hooks and CI run from environments where dotnet may be installed but not on
# PATH: put $DOTNET_ROOT (default ~/.dotnet) on PATH, or exit 1 naming the caller ($1).
ensure_dotnet() {
  if ! command -v dotnet > /dev/null 2>&1; then
    local dotnet_root="${DOTNET_ROOT:-$HOME/.dotnet}"
    if [ -x "$dotnet_root/dotnet" ]; then
      export PATH="$dotnet_root:$PATH"
    else
      echo "$1: the .NET SDK is missing: no dotnet on PATH or in $dotnet_root (install SDK 10.0.403)." >&2
      exit 1
    fi
  fi
}
