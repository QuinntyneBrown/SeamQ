#!/usr/bin/env bash

# ============================================================
# Verify SeamQ environment setup (macOS / Linux)
# ============================================================

echo "============================================================"
echo " SeamQ Environment Verification"
echo "============================================================"
echo

PASS=0; FAIL=0; WARN=0

check_required() {
    if command -v "$1" &>/dev/null; then
        VER=$($2 2>&1 | head -1)
        printf "[PASS] %-18s %s\n" "$3" "$VER"
        ((PASS++))
    else
        printf "[FAIL] %-18s not found\n" "$3"
        ((FAIL++))
    fi
}

check_optional() {
    if command -v "$1" &>/dev/null; then
        VER=$($2 2>&1 | head -1)
        printf "[PASS] %-18s %s\n" "$3" "$VER"
        ((PASS++))
    else
        printf "[WARN] %-18s not found (%s)\n" "$3" "$4"
        ((WARN++))
    fi
}

check_required "dotnet" "dotnet --version" ".NET SDK"
check_optional "node" "node --version" "Node.js" "needed for Angular workspaces"
check_optional "npm" "npm --version" "npm" "comes with Node.js"
check_optional "java" "java -version" "Java" "needed for PlantUML rendering"
check_optional "dot" "dot -V" "Graphviz" "needed for PlantUML class diagrams"
check_optional "git" "git --version" "Git" "version control"

# PlantUML check
PUML_JAR="${PLANTUML_JAR:-$HOME/.plantuml/plantuml.jar}"
if [ -f "$PUML_JAR" ]; then
    printf "[PASS] %-18s %s\n" "PlantUML" "$PUML_JAR"
    ((PASS++))
elif command -v plantuml &>/dev/null; then
    printf "[PASS] %-18s %s\n" "PlantUML" "$(which plantuml)"
    ((PASS++))
else
    printf "[WARN] %-18s not found (needed for diagram rendering)\n" "PlantUML"
    ((WARN++))
fi

check_required "seamq" "seamq --version" "SeamQ"

echo
echo "============================================================"
echo " Results:  $PASS passed, $FAIL failed, $WARN warnings"
echo "============================================================"

if [ "$FAIL" -gt 0 ]; then
    echo
    echo " Required dependencies are missing. Run setup-all.sh to install."
    exit 1
fi

if [ "$WARN" -gt 0 ]; then
    echo
    echo " Optional dependencies missing. SeamQ will work but some features"
    echo " (diagram rendering) will be limited."
fi

echo
exit 0
