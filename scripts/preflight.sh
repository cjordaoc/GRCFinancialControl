#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<USAGE
Usage: $(basename "$0") [--fix]

Checks that the local development prerequisites are available. With --fix it
attempts to install any missing tooling in repository-local folders so the
repository can build on a clean Linux machine.
USAGE
}

log_info() { printf '\e[32m[INFO]\e[0m %s\n' "$1"; }
log_warn() { printf '\e[33m[WARN]\e[0m %s\n' "$1"; }
log_error() { printf '\e[31m[ERROR]\e[0m %s\n' "$1"; }

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
CACHE_DIR="${REPO_ROOT}/.cache/preflight"
DOTNET_ROOT_LOCAL="${REPO_ROOT}/.dotnet"
DOTNET_VERSION="8.0.120"
DOTNET_INSTALL_URI="https://dot.net/v1/dotnet-install.sh"

mkdir -p "${CACHE_DIR}"

fix_mode=false
if (( $# > 1 )); then
  usage
  exit 1
fi

if (( $# == 1 )); then
  case "$1" in
    --fix)
      fix_mode=true
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      usage
      exit 1
      ;;
  esac
fi

ensure_command() {
  local cmd="$1"
  if ! command -v "$cmd" >/dev/null 2>&1; then
    if [[ "$fix_mode" == true ]]; then
      log_error "Required command '${cmd}' is not available. Please install it via your package manager before rerunning the preflight script."
    fi
    return 1
  fi
  return 0
}

ensure_prereq_commands() {
  local missing=false
  for tool in bash curl tar; do
    if ! ensure_command "$tool"; then
      missing=true
    fi
  done
  if [[ "$missing" == true ]]; then
    log_error "Missing required shell dependencies."
    [[ "$fix_mode" == true ]] && exit 1
  fi
}

install_dotnet() {
  local installer_path="${CACHE_DIR}/dotnet-install.sh"
  if [[ ! -f "$installer_path" ]]; then
    log_info "Downloading dotnet-install.sh from ${DOTNET_INSTALL_URI}"
    curl -fsSL "${DOTNET_INSTALL_URI}" -o "$installer_path"
    chmod +x "$installer_path"
  fi
  log_info "Installing .NET SDK ${DOTNET_VERSION} to ${DOTNET_ROOT_LOCAL}"
  bash "$installer_path" --version "${DOTNET_VERSION}" --install-dir "${DOTNET_ROOT_LOCAL}" --no-path
}

verify_dotnet() {
  local dotnet_cmd
  if command -v dotnet >/dev/null 2>&1; then
    dotnet_cmd="$(command -v dotnet)"
  elif [[ -x "${DOTNET_ROOT_LOCAL}/dotnet" ]]; then
    dotnet_cmd="${DOTNET_ROOT_LOCAL}/dotnet"
  else
    return 1
  fi
  log_info "Verifying .NET SDK installation via '${dotnet_cmd} --info'"
  DOTNET_ROOT="${DOTNET_ROOT_LOCAL}" PATH="${DOTNET_ROOT_LOCAL}:${PATH}" "${dotnet_cmd}" --info >/dev/null
  return 0
}

ensure_dotnet() {
  if verify_dotnet; then
    return 0
  fi

  if [[ "$fix_mode" == true ]]; then
    install_dotnet
    if verify_dotnet; then
      log_info "Successfully installed .NET SDK ${DOTNET_VERSION}."
      log_info "Export DOTNET_ROOT=${DOTNET_ROOT_LOCAL} and prepend it to PATH before building."
      return 0
    fi
  fi

  log_warn ".NET SDK ${DOTNET_VERSION} not found. Run with --fix to install it locally."
  return 1
}

main() {
  ensure_prereq_commands

  local failures=0
  if ! ensure_dotnet; then
    failures=1
  fi

  if [[ $failures -ne 0 ]]; then
    exit 1
  fi

  log_info "Preflight checks completed successfully."
}

main "$@"
