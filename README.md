# SharpCMLoot (SCML)

_Absolutely zero guarantee that this will work_

A C# port of [cmloot](https://github.com/shelltrail/cmloot) with the beginnings of snaffler integration.

## Compilation

Open `SCML.sln` in Visual Studio and build in Release mode. The build creates `SCML_Standalone.exe` which includes all dependencies.

## Usage

```cmd
SCML.exe --host <server> [options]
```

## Core Options

| Option | Description |
|--------|-------------|
| `--host <server>` | Target SCCM server |
| `--findsccmservers --domain <DC>` | Discover SCCM servers via LDAP |
| `--list-shares` | List available shares on target |
| `--outfile <file>` | Save inventory to file |
| `--current-user` | Use current Windows credentials |
| `--username <user> --domain <domain>` | Specify credentials |

## Analysis Options

| Option | Description |
|--------|-------------|
| `--snaffler` | Analyse files on share without downloading |
| `--snaffler-inventory <file>` | Analyse existing inventory file |
| `--download-extensions <ext1,ext2>` | Download specific file types |
| `--single-file <path>` | Download single file by UNC path |

## Examples

```cmd
# Discover SCCM servers
SCML.exe --findsccmservers --domain corp.local

# List shares
SCML.exe --host sccm01 --list-shares --current-user

# Create inventory
SCML.exe --host sccm01 --outfile inventory.txt --current-user

# Analyse without downloading
SCML.exe --host sccm01 --snaffler --outfile inventory.txt

# Download specific extensions
SCML.exe --host sccm01 --outfile inventory.txt --download-extensions xml,ps1,config

# Download single file
SCML.exe --single-file \\sccm01\SCCMContentLib$\DataLib\file.xml
```

## Output

- Inventory files contain full UNC paths
- Snaffler analysis creates `_snaffler.txt` and `_snaffler.csv` files
- Downloaded files saved to `CMLootOut` directory by default
