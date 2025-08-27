#!/bin/bash

# Build script to create a single self-contained SCML.exe
echo "=========================================="
echo "Building SCML as single executable"
echo "=========================================="

# Clean previous builds
echo "[*] Cleaning previous builds..."
rm -f bin/Release/SCML_Standalone.exe 2>/dev/null

# Build the solution in Release mode
echo "[*] Building SCML solution..."
msbuild SCML.sln /p:Configuration=Release /p:Platform="Any CPU" /t:Rebuild /v:minimal

if [ $? -ne 0 ]; then
    echo "[-] Build failed!"
    exit 1
fi

# Find ILRepack
ILREPACK=""
if [ -f "packages/ILRepack.2.0.44/tools/ILRepack.exe" ]; then
    ILREPACK="packages/ILRepack.2.0.44/tools/ILRepack.exe"
elif [ -f "bin/Release/packages/ILRepack.2.0.44/tools/ILRepack.exe" ]; then
    ILREPACK="bin/Release/packages/ILRepack.2.0.44/tools/ILRepack.exe"
else
    echo "[-] ILRepack not found. Installing..."
    nuget install ILRepack -OutputDirectory packages
    ILREPACK="packages/ILRepack.2.0.44/tools/ILRepack.exe"
fi

echo "[*] Using ILRepack from: $ILREPACK"

# Navigate to output directory
cd bin/Release

# Merge all assemblies into a single executable
echo "[*] Merging assemblies..."
mono ../../$ILREPACK \
    /out:SCML_Standalone.exe \
    /targetplatform:v4 \
    /internalize \
    /parallel \
    /wildcards \
    SCML.exe \
    CommandLine.dll \
    Newtonsoft.Json.dll \
    SMBLibrary.dll \
    System.Buffers.dll \
    System.Memory.dll \
    System.Numerics.Vectors.dll \
    System.Runtime.CompilerServices.Unsafe.dll

if [ $? -eq 0 ]; then
    echo "[+] Successfully created SCML_Standalone.exe"
    echo "[*] File size: $(du -h SCML_Standalone.exe | cut -f1)"
    
    # Test the merged executable
    echo ""
    echo "[*] Testing merged executable..."
    mono SCML_Standalone.exe --help > /dev/null 2>&1
    if [ $? -eq 0 ]; then
        echo "[+] Merged executable works correctly!"
        echo ""
        echo "=========================================="
        echo "Build complete: bin/Release/SCML_Standalone.exe"
        echo "This is a self-contained executable that includes all dependencies"
        echo "=========================================="
    else
        echo "[-] Merged executable test failed!"
        exit 1
    fi
else
    echo "[-] Merging failed!"
    exit 1
fi

cd ../..