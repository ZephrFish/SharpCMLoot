#!/bin/bash

echo "SCML Enhanced Testing Script"
echo "============================"
echo ""

# Test 1: List shares on discovered SCCM server
echo "[Test 1] List all shares on SCCM server:"
echo "Command: mono SCML.exe --host SCCM-MGMT.YXZ.RED --list-shares --debug --username domainadmin --password 'Passw0rd1!' --domain yxz.red"
echo ""

# Test 2: Try inventory with auto-detection
echo "[Test 2] Create inventory with share auto-detection:"
echo "Command: mono SCML.exe --host SCCM-MGMT.YXZ.RED --outfile inventory.txt --debug --username domainadmin --password 'Passw0rd1!' --domain yxz.red"
echo ""

# Test 3: Try with current user (if domain joined)
echo "[Test 3] Try with current user credentials:"
echo "Command: mono SCML.exe --host SCCM-MGMT.YXZ.RED --outfile inventory.txt --debug --current-user"
echo ""

echo "Choose a test to run (1-3) or press Enter to exit:"
read choice

case $choice in
    1)
        mono bin/Release/SCML.exe --host SCCM-MGMT.YXZ.RED --list-shares --debug --username domainadmin --password 'Passw0rd1!' --domain yxz.red
        ;;
    2)
        mono bin/Release/SCML.exe --host SCCM-MGMT.YXZ.RED --outfile inventory.txt --debug --username domainadmin --password 'Passw0rd1!' --domain yxz.red
        ;;
    3)
        mono bin/Release/SCML.exe --host SCCM-MGMT.YXZ.RED --outfile inventory.txt --debug --current-user
        ;;
    *)
        echo "No test selected."
        ;;
esac