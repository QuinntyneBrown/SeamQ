#!/bin/bash
# SeamQ iteration script: clean, rebuild, install, generate, verify
set -e

OUTDIR="C:/Users/quinn/Downloads/out"
PROJECT="C:/projects/Dashboard/src/Dashboard.Web"
SEAMQ_DIR="C:/projects/SeamQ"

for i in $(seq 1 $1); do
    echo "=== ITERATION $i ==="

    # 1. Clean Downloads
    rm -rf "C:/Users/quinn/Downloads"/* 2>/dev/null || true
    mkdir -p "$OUTDIR"

    # 2. Rebuild and reinstall
    cd "$SEAMQ_DIR"
    cmd //c "C:\\projects\\SeamQ\\eng\\scripts\\rebuild-install.bat" > /dev/null 2>&1
    echo "  [OK] Rebuilt and installed"

    # 3. Scan
    seamq scan "$PROJECT" --output-dir "$OUTDIR" --save-baseline "$OUTDIR/baseline.json" --quiet 2>/dev/null
    echo "  [OK] Scanned"

    # 4. Generate all docs
    seamq generate --all --format md --output-dir "$OUTDIR" --quiet 2>/dev/null
    seamq generate --all --format html --output-dir "$OUTDIR" --quiet 2>/dev/null
    seamq diagram --all --output-dir "$OUTDIR" --quiet 2>/dev/null
    seamq validate --all --output-dir "$OUTDIR" --quiet 2>/dev/null
    seamq export --all --output-dir "$OUTDIR" --quiet 2>/dev/null
    echo "  [OK] Generated all docs"

    # 5. Count and verify diagrams
    TOTAL_FILES=$(find "$OUTDIR" -name "*.puml" | wc -l)
    SEQ_FILES=$(find "$OUTDIR" -name "*Seq*.puml" | wc -l)
    CLASS_FILES=$(find "$OUTDIR" -name "*Class*.puml" | wc -l)
    C4_FILES=$(find "$OUTDIR" -name "*C4*.puml" | wc -l)
    MD_FILES=$(find "$OUTDIR" -name "*.md" | wc -l)
    HTML_FILES=$(find "$OUTDIR" -name "*.html" | wc -l)
    JSON_FILES=$(find "$OUTDIR" -name "*.json" | wc -l)

    echo "  Diagrams: $TOTAL_FILES total ($CLASS_FILES class, $SEQ_FILES sequence, $C4_FILES C4)"
    echo "  Docs: $MD_FILES md, $HTML_FILES html, $JSON_FILES json"

    # 6. Verify diagrams have content (not just startuml/enduml)
    EMPTY_COUNT=0
    for f in $(find "$OUTDIR" -name "*.puml"); do
        LINES=$(wc -l < "$f")
        if [ "$LINES" -lt 6 ]; then
            EMPTY_COUNT=$((EMPTY_COUNT + 1))
            echo "  [WARN] Near-empty diagram: $(basename $f) ($LINES lines)"
        fi
    done

    if [ "$EMPTY_COUNT" -eq 0 ]; then
        echo "  [OK] All diagrams have content"
    else
        echo "  [WARN] $EMPTY_COUNT near-empty diagrams"
    fi

    echo ""
done

echo "=== ALL $1 ITERATIONS COMPLETE ==="
