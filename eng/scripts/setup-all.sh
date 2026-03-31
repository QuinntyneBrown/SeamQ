#!/usr/bin/env bash
set -euo pipefail

# ============================================================
# SeamQ Full Environment Setup (macOS / Linux)
# Installs all dependencies from scratch
# ============================================================

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"

echo "============================================================"
echo " SeamQ Environment Setup"
echo "============================================================"
echo

# Detect package manager
if command -v brew &>/dev/null; then
    PKG="brew"
elif command -v apt-get &>/dev/null; then
    PKG="apt"
elif command -v dnf &>/dev/null; then
    PKG="dnf"
else
    PKG="unknown"
fi

echo "Detected package manager: $PKG"
echo

# --- .NET SDK ---
echo "[1/6] .NET SDK 8.0 ..."
if command -v dotnet &>/dev/null; then
    echo "  Found: $(dotnet --version)"
else
    if [ "$PKG" = "brew" ]; then
        brew install --cask dotnet-sdk
    elif [ "$PKG" = "apt" ]; then
        wget https://dot.net/v1/dotnet-install.sh -O /tmp/dotnet-install.sh
        chmod +x /tmp/dotnet-install.sh
        /tmp/dotnet-install.sh --channel 8.0
        export PATH="$HOME/.dotnet:$PATH"
    elif [ "$PKG" = "dnf" ]; then
        sudo dnf install -y dotnet-sdk-8.0
    else
        echo "  Download from: https://dotnet.microsoft.com/download/dotnet/8.0"
    fi
fi

# --- Node.js ---
echo "[2/6] Node.js LTS ..."
if command -v node &>/dev/null; then
    echo "  Found: $(node --version)"
else
    if [ "$PKG" = "brew" ]; then
        brew install node
    elif [ "$PKG" = "apt" ]; then
        curl -fsSL https://deb.nodesource.com/setup_22.x | sudo -E bash -
        sudo apt-get install -y nodejs
    elif [ "$PKG" = "dnf" ]; then
        sudo dnf install -y nodejs
    else
        echo "  Download from: https://nodejs.org/"
    fi
fi

# --- Java ---
echo "[3/6] Java JDK 21 ..."
if command -v java &>/dev/null; then
    echo "  Found: $(java -version 2>&1 | head -1)"
else
    if [ "$PKG" = "brew" ]; then
        brew install --cask temurin
    elif [ "$PKG" = "apt" ]; then
        sudo apt-get install -y temurin-21-jdk || sudo apt-get install -y default-jdk
    elif [ "$PKG" = "dnf" ]; then
        sudo dnf install -y java-21-openjdk
    else
        echo "  Download from: https://adoptium.net/"
    fi
fi

# --- Graphviz ---
echo "[4/6] Graphviz ..."
if command -v dot &>/dev/null; then
    echo "  Found: $(dot -V 2>&1)"
else
    if [ "$PKG" = "brew" ]; then
        brew install graphviz
    elif [ "$PKG" = "apt" ]; then
        sudo apt-get install -y graphviz
    elif [ "$PKG" = "dnf" ]; then
        sudo dnf install -y graphviz
    else
        echo "  Download from: https://graphviz.org/download/"
    fi
fi

# --- PlantUML ---
echo "[5/6] PlantUML ..."
PUML_DIR="$HOME/.plantuml"
PUML_JAR="$PUML_DIR/plantuml.jar"
if [ -f "$PUML_JAR" ]; then
    echo "  Found: $PUML_JAR"
elif command -v plantuml &>/dev/null; then
    echo "  Found: $(which plantuml)"
else
    if [ "$PKG" = "brew" ]; then
        brew install plantuml
    else
        mkdir -p "$PUML_DIR"
        echo "  Downloading plantuml.jar ..."
        curl -fsSL -o "$PUML_JAR" "https://github.com/plantuml/plantuml/releases/download/v1.2025.2/plantuml-1.2025.2.jar" || true
        if [ -f "$PUML_JAR" ]; then
            echo "  Installed: $PUML_JAR"
            # Set env var
            echo "export PLANTUML_JAR=\"$PUML_JAR\"" >> "$HOME/.bashrc"
            export PLANTUML_JAR="$PUML_JAR"
        else
            echo "  Download failed. Get it from: https://plantuml.com/download"
        fi
    fi
fi

# --- SeamQ ---
echo "[6/6] SeamQ CLI ..."
if command -v seamq &>/dev/null; then
    echo "  Found: $(seamq --version)"
    dotnet tool update --global SeamQ || true
else
    dotnet tool install --global SeamQ
fi

echo
echo "============================================================"
echo " Setup Complete!"
echo "============================================================"
echo
bash "$SCRIPT_DIR/verify-setup.sh" || true
