#!/usr/bin/env bash
# Repair the OpenLore tree-sitter grammars needed to graph C# (and Bash) source.
#
# WHY: OpenLore loads most grammars as native Node addons resolved from the
# global openlore install. Two things can break that on this repo:
#   1. A corrupted/partial pnpm store entry leaves `node_modules/tree-sitter-*`
#      empty, so `openlore analyze` logs "language C# grammar unavailable".
#   2. `tree-sitter-c-sharp@0.21.3` ships prebuilt binaries for darwin-x64,
#      win32-x64, linux-x64 and darwin-arm64 but NOT linux-arm64 (Apple Silicon
#      devcontainers). Without a prebuild the grammar cannot load and C# files
#      are indexed for search but never graphed (no call graph, no orient hits).
#
# The fix is deterministic and idempotent: repoint the openlore store symlink
# to a complete grammar entry (if it is broken), then compile the native
# binding from the grammar's own source into `build/Release/` — node-gyp-build
# checks `build/Release/*.node` before prebuilds, so the compiled binding is
# picked up even on platforms with no published prebuild.
#
# Requires: gcc, g++ (build-essential), and the Node headers matching the
# active Node runtime (bundled with the pnpm-managed node when one is used).
set -euo pipefail

log() { printf '\033[1;36m[repair-openlore]\033[0m %s\n' "$*"; }
die() { printf '\033[1;31m[repair-openlore]\033[0m ERROR: %s\n' "$*" >&2; exit 1; }

# --- Locate the global openlore package directory ----------------------------
if ! command -v openlore >/dev/null 2>&1; then
    die "openlore not found on PATH. Install it first: pnpm install -g openlore"
fi

BIN_DIR="$(dirname "$(readlink -f "$(command -v openlore)")")"
# pnpm global shims embed the real package entry via a trailing
# `# cmd-shim-target=<path>` comment; strip the dist entry to get the package root.
OL_PKG="$(sed -n 's/^# cmd-shim-target=\(.*\)\/dist\/cli\/index\.js$/\1/p' "$(command -v openlore)" | head -n1)"
if [ -z "$OL_PKG" ] || [ ! -f "$OL_PKG/package.json" ]; then
    OL_PKG="$(node -e "
      try {
        const path = require('path');
        console.log(path.dirname(require.resolve('openlore/package.json', { paths: ['$BIN_DIR'] })));
      } catch { process.exit(1); }
    " 2>/dev/null)" || true
fi
# Follow symlinks: pnpm global shims point at a symlinked store entry, and the
# grammar deps live in the store's virtual node_modules, not the shim dir.
OL_PKG="$(readlink -f "$OL_PKG" 2>/dev/null || echo "$OL_PKG")"
[ -n "$OL_PKG" ] && [ -f "$OL_PKG/package.json" ] || die "could not resolve the openlore package (shim: $(command -v openlore))"

log "openlore package: $OL_PKG"

# The pnpm virtual store dir whose node_modules holds the grammar symlinks.
VSTORE="$(dirname "$(dirname "$OL_PKG")")"   # .../node_modules/openlore -> .../node_modules
[ -d "$VSTORE/node_modules" ] || VSTORE="$(dirname "$VSTORE")"

NODE_INC="$(node -e "
  const path = require('path');
  const fs = require('fs');
  const candidates = [
    path.join(path.dirname(process.execPath), '..', 'include', 'node'),
    path.join(process.execPath, '..', '..', 'include', 'node'),
  ];
  for (const c of candidates) if (fs.existsSync(path.join(c, 'node_api.h'))) { console.log(c); process.exit(0); }
  process.exit(1);
" 2>/dev/null)" || die "could not locate Node headers (node_api.h); install a node with headers"

log "node headers: $NODE_INC"

# --- Locate the symlink openlore resolves a grammar through -------------------
grammar_link() {
    local name="$1"
    local link="$VSTORE/node_modules/$name"
    [ -L "$link" ] && echo "$link" || { [ -d "$link" ] && echo "$link"; }
}

# --- Search the pnpm store for a complete grammar package ---------------------
store_candidates() {
    local name="$1"
    local store_root="${PNPM_STORE_PATH:-$HOME/.local/share/pnpm/store}"
    [ -d "$store_root" ] || store_root="$(pnpm store path 2>/dev/null || true)"
    [ -d "${store_root:-}" ] || return 1
    find "$store_root" -type d -path "*node_modules/$name" 2>/dev/null | sort -r
}

# --- Repoint the openlore grammar symlink to a complete store entry -----------
repair_store_link() {
    local name="$1"
    local link
    link="$(grammar_link "$name")" || true
    [ -n "${link:-}" ] || return 1

    # If the current target already carries grammar source, nothing to repoint.
    if [ -f "$link/package.json" ] && [ -d "$link/src" ]; then
        return 0
    fi

    log "$name: linked entry is incomplete — searching pnpm store for a complete copy"
    local entry=""
    while IFS= read -r cand; do
        if [ -f "$cand/package.json" ] && [ -d "$cand/src" ]; then
            entry="$cand"
            break
        fi
    done < <(store_candidates "$name" || true)

    if [ -z "$entry" ]; then
        log "$name: no complete store entry found"
        return 1
    fi

    log "$name: repointing $link -> $entry"
    rm -f "$link"
    ln -s "$entry" "$link"
}

# --- Resolve a grammar package root (entry file's ancestor with package.json) --
resolve_grammar() {
    local name="$1"
    node -e "
      const path = require('path');
      const fs = require('fs');
      try {
        let p = require.resolve('$name', { paths: ['$VSTORE/node_modules'] });
        while (p !== '/' && !fs.existsSync(path.join(p, 'package.json'))) p = path.dirname(p);
        console.log(p);
      } catch { process.exit(1); }
    " 2>/dev/null
}

# --- Build a grammar's native binding from source (if needed) -----------------
# node-gyp-build resolves in this order: build/Release/*.node, build/Debug/*.node,
# prebuilds/<platform>-<arch>/. So a compiled binding always wins over prebuilds.
build_grammar() {
    local name="$1"
    local pkg_dir target_name work platform_arch

    pkg_dir="$(resolve_grammar "$name")"
    [ -n "$pkg_dir" ] && [ -f "$pkg_dir/package.json" ] || {
        log "$name: cannot resolve a package root — skipping"
        return 1
    }

    [ -f "$pkg_dir/binding.gyp" ] || die "$name: no binding.gyp in $pkg_dir"
    # binding.gyp is gyp syntax (comments like `# OS == "win"`), not strict JSON,
    # so JSON.parse would throw. Extract the first "target_name" instead.
    target_name="$(sed -n 's/^[[:space:]]*"target_name"[[:space:]]*:[[:space:]]*"\([^"]*\)".*$/\1/p' "$pkg_dir/binding.gyp" | head -n1)"
    [ -n "$target_name" ] || die "$name: could not read target_name from binding.gyp"

    work="$pkg_dir/build/Release"
    if [ -f "$work/$target_name.node" ]; then
        log "$name: native binding already present, nothing to build"
        return 0
    fi

    # node-addon-api is an npm dependency of the grammar; locate it from the
    # grammar's own node_modules chain, falling back to the openlore install.
    NAA="$(node -e "
      const path = require('path');
      const fs = require('fs');
      for (const base of ['$pkg_dir', '$OL_PKG']) {
        try {
          const p = path.dirname(require.resolve('node-addon-api/package.json', { paths: [base] }));
          // v8+ keeps napi.h at the package root; older versions keep it in include/
          for (const inc of [p, path.join(p, 'include')]) {
            if (fs.existsSync(path.join(inc, 'napi.h'))) { console.log(inc); process.exit(0); }
          }
        } catch {}
      }
      process.exit(1);
    " 2>/dev/null)" || die "$name: could not locate node-addon-api headers"

    platform_arch="$(node -e 'process.stdout.write(process.platform + "-" + process.arch)')"
    log "$name: compiling $target_name from source (no usable prebuild for $platform_arch)"
    mkdir -p "$work"

    gcc  -std=c11  -fPIC -O2 -I "$pkg_dir/src" -c "$pkg_dir/src/parser.c"   -o "$work/parser.o"
    if [ -f "$pkg_dir/src/scanner.c" ]; then
        gcc -std=c11  -fPIC -O2 -I "$pkg_dir/src" -c "$pkg_dir/src/scanner.c" -o "$work/scanner.o"
    else
        : > "$work/scanner.o"   # some grammars have no scanner; keep linker happy
    fi
    g++  -std=c++17 -fPIC -O2 -I "$pkg_dir/src" -I "$NAA" -I "$NODE_INC" \
        -c "$pkg_dir/bindings/node/binding.cc" -o "$work/binding.o"

    g++ -shared -o "$work/$target_name.node" "$work/parser.o" "$work/scanner.o" "$work/binding.o"
    rm -f "$work/parser.o" "$work/scanner.o" "$work/binding.o"
    log "$name: built $work/$target_name.node"
}

# --- Verify a grammar loads ---------------------------------------------------
verify_grammar() {
    local name="$1"
    if (cd "$OL_PKG" && node --input-type=module -e "import('$name').then(()=>process.exit(0)).catch(()=>process.exit(1))"); then
        log "$name: loads OK"
        return 0
    fi
    log "$name: still fails to load"
    return 1
}

main() {
    local fixed_any=0
    local failed_any=0
    for grammar in tree-sitter-c-sharp tree-sitter-bash; do
        log "== $grammar =="
        # Fast path: grammar already loads (correct store entry + usable prebuild
        # or previously compiled binding) — nothing to do.
        if verify_grammar "$grammar"; then
            continue
        fi
        log "$grammar does not load — repairing"
        repair_store_link "$grammar" || true
        if build_grammar "$grammar" && verify_grammar "$grammar"; then
            fixed_any=1
        else
            log "$grammar still does not load — see messages above"
            failed_any=1
        fi
    done
    if [ "$failed_any" -ne 0 ]; then
        die "one or more OpenLore grammars could not be repaired"
    elif [ "$fixed_any" -eq 1 ]; then
        log "Repairs applied. Re-run: openlore analyze --force && openlore doctor"
    else
        log "Nothing to repair — OpenLore grammars are healthy."
    fi
}

main "$@"
