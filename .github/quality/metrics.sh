#!/usr/bin/env bash
# Prints the quality-loop size metrics as a Markdown table. Run from the repo root.
set -euo pipefail

cs_loc() { find "$@" -name '*.cs' -not -path '*/obj/*' -not -path '*/bin/*' -print0 | xargs -0 cat | wc -l | tr -d ' '; }
public_types() { grep -rhE '^\s*public (sealed |static |abstract |partial |readonly |record |ref )*(class|interface|record|struct|enum|delegate) ' "$@" --include='*.cs' | wc -l | tr -d ' '; }
test_count() { grep -rhoE '\[(Fact|Theory)' "$@" --include='*.cs' | wc -l | tr -d ' '; }
md_lines() { find "$@" -name '*.md' -not -path '*/node_modules/*' -print0 2>/dev/null | xargs -0 cat 2>/dev/null | wc -l | tr -d ' '; }

echo "| Metric | Value |"
echo "|---|---|"
echo "| Commit | \`$(git rev-parse --short HEAD)\` |"
echo "| src LOC | $(cs_loc src) |"
echo "| Public types | $(public_types src) |"
for p in src/*/; do echo "| &nbsp;&nbsp;$(basename "$p") | $(public_types "$p") types, $(cs_loc "$p") LOC |"; done
echo "| Packable projects | $(grep -L '<IsPackable>false' src/*/*.csproj | wc -l | tr -d ' ') |"
echo "| tests LOC | $(cs_loc tests) |"
echo "| Test methods ([Fact]/[Theory]) | $(test_count tests) |"
echo "| specs/ lines | $(md_lines specs) |"
echo "| Markdown lines (excl. specs) | $(( $(md_lines .) - $(md_lines specs) )) |"
