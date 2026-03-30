#!/usr/bin/env bash
# run-verification.sh — 100-iteration verification loop for SeamQ CLI
# Generates fixtures, runs CLI, audits output, logs gaps
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
TEMP_BASE="${TEMP:-/tmp}/seamq-verify"
LOG_FILE="$PROJECT_ROOT/eng/verification-log.md"
GAP_FILE="$PROJECT_ROOT/eng/gaps.md"
MAX_ITER="${1:-100}"
START_ITER="${2:-1}"

# Reinstall the CLI from source
reinstall_cli() {
  pushd "$PROJECT_ROOT" > /dev/null
  dotnet pack src/SeamQ.Cli/SeamQ.Cli.csproj -o ./nupkg --force -v quiet 2>/dev/null
  dotnet tool update --global --add-source ./nupkg SeamQ 2>/dev/null || \
    dotnet tool install --global --add-source ./nupkg SeamQ 2>/dev/null || true
  popd > /dev/null
}

# Initialize log files
init_logs() {
  cat > "$LOG_FILE" <<HDR
# SeamQ Verification Log
Generated: $(date -u +"%Y-%m-%dT%H:%M:%SZ")
Iterations: ${START_ITER}–${MAX_ITER}

| Iter | Seams Found | ICDs Generated | Diagrams Generated | Scan OK | Gen OK | Diag OK | Gaps |
|------|-------------|----------------|-------------------|---------|--------|---------|------|
HDR

  cat > "$GAP_FILE" <<HDR
# SeamQ Verification Gaps
Generated: $(date -u +"%Y-%m-%dT%H:%M:%SZ")

HDR
}

# Audit a single iteration's output
# Returns the number of gaps found (appended to GAP_FILE)
audit_output() {
  local iter="$1" output_dir="$2" scan_exit="$3" gen_exit="$4" diag_exit="$5"
  local gaps=0
  local iter_gaps=""

  # G1: scan must succeed
  if [[ "$scan_exit" != "0" ]]; then
    iter_gaps+="  - **SCAN_FAIL**: seamq scan exited with code ${scan_exit}\n"
    ((gaps++)) || true
  fi

  # G2: must detect at least 1 seam
  # Registry is written to CWD/.seamq/registry.json — CWD is the iter dir during scan
  local iter_base
  iter_base="$(dirname "$output_dir")"
  local registry_file="$iter_base/.seamq/registry.json"
  local seam_count=0
  if [[ -f "$registry_file" ]]; then
    # Count top-level array items
    seam_count=$(python3 -c "import json,sys; d=json.load(open(sys.argv[1])); print(len(d))" "$registry_file" 2>/dev/null || echo 0)
  fi
  if (( seam_count == 0 )); then
    iter_gaps+="  - **NO_SEAMS**: No seams detected by scan\n"
    ((gaps++)) || true
  fi

  # G3: generate must succeed
  if [[ "$gen_exit" != "0" ]]; then
    iter_gaps+="  - **GEN_FAIL**: seamq generate --all exited with code ${gen_exit}\n"
    ((gaps++)) || true
  fi

  # G4: diagram must succeed
  if [[ "$diag_exit" != "0" ]]; then
    iter_gaps+="  - **DIAG_FAIL**: seamq diagram --all exited with code ${diag_exit}\n"
    ((gaps++)) || true
  fi

  # G5: check ICD markdown files exist
  local icd_count=0
  if [[ -d "$output_dir" ]]; then
    icd_count=$(find "$output_dir" -name "*.md" -type f 2>/dev/null | wc -l)
  fi
  if (( icd_count == 0 )); then
    iter_gaps+="  - **NO_ICD_FILES**: No .md ICD files generated in ${output_dir}\n"
    ((gaps++)) || true
  fi

  # G6: check diagram .puml files exist
  local puml_count=0
  if [[ -d "$output_dir" ]]; then
    puml_count=$(find "$output_dir" -name "*.puml" -type f 2>/dev/null | wc -l)
  fi
  if (( puml_count == 0 )); then
    iter_gaps+="  - **NO_DIAGRAMS**: No .puml diagram files generated\n"
    ((gaps++)) || true
  fi

  # G7: ICD content checks (only if files exist)
  if (( icd_count > 0 )); then
    local sample_icd
    sample_icd=$(find "$output_dir" -name "*.md" -type f | head -1)

    # Check for key ICD sections from PRD
    local sections=("Introduction" "Interface" "Contract" "Traceability")
    for section in "${sections[@]}"; do
      if ! grep -qi "$section" "$sample_icd" 2>/dev/null; then
        iter_gaps+="  - **MISSING_SECTION**: ICD missing section containing '${section}'\n"
        ((gaps++)) || true
      fi
    done

    # Check ICD is not trivially small
    local icd_lines
    icd_lines=$(wc -l < "$sample_icd")
    if (( icd_lines < 20 )); then
      iter_gaps+="  - **THIN_ICD**: ICD only has ${icd_lines} lines (expected 50+)\n"
      ((gaps++)) || true
    fi
  fi

  # G8: diagram content checks (only if files exist)
  if (( puml_count > 0 )); then
    local has_class=0 has_seq=0 has_c4=0
    find "$output_dir" -name "*.puml" -type f | while read -r f; do
      grep -q "class " "$f" 2>/dev/null && has_class=1
      grep -q "participant " "$f" 2>/dev/null && has_seq=1
      grep -q "C4" "$f" 2>/dev/null && has_c4=1
    done || true
    # These checks are informational for now
  fi

  # G9: at higher iterations, expect more diagram types
  if (( iter >= 20 )); then
    local class_diag_count seq_diag_count c4_diag_count
    class_diag_count=$(find "$output_dir" -name "*class*" -o -name "*api*" 2>/dev/null | wc -l)
    seq_diag_count=$(find "$output_dir" -name "*seq*" -o -name "*lifecycle*" 2>/dev/null | wc -l)
    c4_diag_count=$(find "$output_dir" -name "*c4*" -o -name "*context*" -o -name "*container*" 2>/dev/null | wc -l)

    if (( class_diag_count == 0 )); then
      iter_gaps+="  - **NO_CLASS_DIAGRAMS**: Expected class diagrams at iter ${iter}\n"
      ((gaps++)) || true
    fi
  fi

  # Write gaps for this iteration
  if (( gaps > 0 )); then
    echo -e "\n### Iteration ${iter} (${gaps} gaps)\n${iter_gaps}" >> "$GAP_FILE"
  fi

  echo "$gaps"
  echo "$seam_count"
  echo "$icd_count"
  echo "$puml_count"
}

########################################################################
# MAIN LOOP
########################################################################
echo "=== SeamQ 100-Iteration Verification ==="
echo "Project: $PROJECT_ROOT"
echo "Temp: $TEMP_BASE"
echo "Log: $LOG_FILE"
echo "Iterations: ${START_ITER}–${MAX_ITER}"
echo ""

init_logs

echo "Installing CLI..."
reinstall_cli
echo "CLI installed."
echo ""

total_gaps=0
total_seams=0
total_icds=0
total_pumls=0

for iter in $(seq "$START_ITER" "$MAX_ITER"); do
  ITER_DIR="$TEMP_BASE/iter-${iter}"
  OUTPUT_DIR="$ITER_DIR/seamq-output"

  echo -n "Iter ${iter}/${MAX_ITER}: "

  # Step 1: Generate fixtures
  mkdir -p "$ITER_DIR"
  bash "$SCRIPT_DIR/generate-fixture.sh" "$iter" "$ITER_DIR" > /dev/null 2>&1

  # Run all CLI commands from the iteration directory so registry + output land there
  pushd "$ITER_DIR" > /dev/null

  # Step 2: Run seamq scan
  scan_exit=0
  seamq scan \
    "$ITER_DIR/lib-workspace-a" \
    "$ITER_DIR/lib-workspace-b" \
    "$ITER_DIR/app-workspace" \
    "$ITER_DIR/standalone-app" \
    --output-dir "$OUTPUT_DIR" \
    --quiet 2>/dev/null || scan_exit=$?

  # Step 3: Run seamq generate --all
  gen_exit=0
  seamq generate --all \
    --output-dir "$OUTPUT_DIR" \
    --quiet 2>/dev/null || gen_exit=$?

  # Step 4: Run seamq diagram --all
  diag_exit=0
  seamq diagram --all \
    --output-dir "$OUTPUT_DIR" \
    --quiet 2>/dev/null || diag_exit=$?

  popd > /dev/null

  # Step 5: Audit output
  audit_results=$(audit_output "$iter" "$OUTPUT_DIR" "$scan_exit" "$gen_exit" "$diag_exit")
  IFS=$'\n' read -r -d '' gap_count seam_count icd_count puml_count <<< "$audit_results" || true

  # Record in log
  scan_ok=$([[ "$scan_exit" == "0" ]] && echo "Y" || echo "N")
  gen_ok=$([[ "$gen_exit" == "0" ]] && echo "Y" || echo "N")
  diag_ok=$([[ "$diag_exit" == "0" ]] && echo "Y" || echo "N")
  echo "| ${iter} | ${seam_count:-0} | ${icd_count:-0} | ${puml_count:-0} | ${scan_ok} | ${gen_ok} | ${diag_ok} | ${gap_count:-0} |" >> "$LOG_FILE"

  total_gaps=$(( total_gaps + ${gap_count:-0} ))
  total_seams=$(( total_seams + ${seam_count:-0} ))
  total_icds=$(( total_icds + ${icd_count:-0} ))
  total_pumls=$(( total_pumls + ${puml_count:-0} ))

  echo "seams=${seam_count:-0} icds=${icd_count:-0} pumls=${puml_count:-0} gaps=${gap_count:-0}"

  # Step 6: Clean up temp workspace
  rm -rf "$ITER_DIR"
done

# Summary
cat >> "$LOG_FILE" <<SUMMARY

## Summary
- **Total iterations**: $((MAX_ITER - START_ITER + 1))
- **Total seams detected**: ${total_seams}
- **Total ICDs generated**: ${total_icds}
- **Total diagrams generated**: ${total_pumls}
- **Total gaps found**: ${total_gaps}
SUMMARY

echo ""
echo "=== Verification Complete ==="
echo "Total gaps: ${total_gaps}"
echo "Log: ${LOG_FILE}"
echo "Gaps: ${GAP_FILE}"
