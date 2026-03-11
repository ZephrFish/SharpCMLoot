# Snaffler SCCM Addon

A powerful addon for Snaffler that discovers SCCM Distribution Points and runs Snaffler analysis against indexed INI files from SCCM Content Libraries.

## Overview

This addon combines:
- **SharpCMLoot's SCCM discovery logic**: LDAP-based server discovery and SMB share enumeration
- **Intelligent INI file indexing**: Parses SCCM's content library structure to map hashed files to original names
- **Snaffler integration**: Runs your Snaffler fork against discovered files with full rule engine support

## Why This Addon?

SCCM Content Libraries store files with hash-based names (e.g., `A1B2C3D4.INI`), making them difficult to identify. This addon:

1. Discovers all SCCM Distribution Points in your domain
2. Indexes the SCCMContentLib$ share structure
3. Parses .INI files to recover original filenames
4. Feeds the indexed files to Snaffler for sensitive data detection
5. Produces enriched reports with both SCCM metadata and Snaffler findings

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    Snaffler SCCM Addon                      │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  Phase 1: Discovery                                        │
│  ┌──────────────────────────────────────────┐             │
│  │ SCCMShareDiscovery                       │             │
│  │  • LDAP queries (3 methods)              │             │
│  │  • Server validation                     │             │
│  │  • Share accessibility check             │             │
│  └──────────────────────────────────────────┘             │
│                    ↓                                        │
│  Phase 2: Indexing                                         │
│  ┌──────────────────────────────────────────┐             │
│  │ SCCMINIIndexer                           │             │
│  │  • Parse DataLib structure               │             │
│  │  • Read .INI files                       │             │
│  │  • Map hash → original filename          │             │
│  │  • Filter by extension/pattern           │             │
│  └──────────────────────────────────────────┘             │
│                    ↓                                        │
│  Phase 3: Analysis                                         │
│  ┌──────────────────────────────────────────┐             │
│  │ SnafflerIntegration                      │             │
│  │  • Prepare target file list              │             │
│  │  • Execute Snaffler.exe                  │             │
│  │  • Parse findings                        │             │
│  │  • Enrich with SCCM metadata             │             │
│  └──────────────────────────────────────────┘             │
│                    ↓                                        │
│  Output: Enriched Reports                                  │
└─────────────────────────────────────────────────────────────┘
```

## Installation

### Prerequisites

- .NET Framework 4.8 or higher
- Windows OS (for LDAP/SMB operations)
- Domain account with SCCM read access
- Snaffler executable (your ZephrFish fork)

### Build

```bash
# Clone/copy to your development environment
cd SnafflerSCCMAddon

# Restore NuGet packages
nuget restore

# Build with Visual Studio or MSBuild
msbuild SnafflerSCCMAddon.csproj /p:Configuration=Release
```

Output: `bin\Release\SnafflerSCCM.exe`

## Usage

### Basic Usage

```bash
# Discover, index, and analyze (all-in-one)
SnafflerSCCM.exe --domain corp.local \
                 --snaffler-exe "C:\Tools\Snaffler.exe" \
                 --output-report sccm_analysis.txt

# With authentication
SnafflerSCCM.exe --domain corp.local \
                 --username sccmreader \
                 --password P@ssw0rd \
                 --snaffler-exe "C:\Tools\Snaffler.exe" \
                 --output-report sccm_analysis.txt
```

### Discovery Only

```bash
# Discover SCCM servers without indexing
SnafflerSCCM.exe --domain corp.local \
                 --output-discovery sccm_servers.txt \
                 --validate

# Use discovered server list
SnafflerSCCM.exe --server-list sccm_servers.txt \
                 --snaffler-exe "C:\Tools\Snaffler.exe"
```

### Target Specific Server

```bash
# Single server analysis
SnafflerSCCM.exe --server sccm01.corp.local \
                 --snaffler-exe "C:\Tools\Snaffler.exe" \
                 --output-snaffler findings.txt
```

### Indexing with Filters

```bash
# Index only specific file types
SnafflerSCCM.exe --domain corp.local \
                 --extensions xml,ini,config,ps1 \
                 --output-index sccm_index.csv \
                 --snaffler-exe "C:\Tools\Snaffler.exe"

# Export index without running Snaffler
SnafflerSCCM.exe --domain corp.local \
                 --output-index sccm_index.csv
```

### Advanced Snaffler Options

```bash
# Custom Snaffler rules and performance tuning
SnafflerSCCM.exe --domain corp.local \
                 --snaffler-exe "C:\Tools\Snaffler.exe" \
                 --rules custom_rules.toml \
                 --threads 50 \
                 --max-size 1024 \
                 --timeout 120 \
                 --stream \
                 --verbose
```

## Command-Line Options

### Discovery Options

| Option | Description |
|--------|-------------|
| `-d, --domain` | Domain for LDAP discovery (e.g., corp.local) |
| `-s, --server` | Single SCCM server to target |
| `--server-list` | File containing list of servers (one per line) |
| `--ldap-port` | LDAP port (default: 389) |
| `--validate` | Validate server accessibility before indexing |

### Authentication Options

| Option | Description |
|--------|-------------|
| `-u, --username` | Username for authentication |
| `-p, --password` | Password for authentication |

### Indexing Options

| Option | Description |
|--------|-------------|
| `-e, --extensions` | Filter by extensions (e.g., xml,ini,config) |

### Snaffler Options

| Option | Description |
|--------|-------------|
| `--snaffler-exe` | Path to Snaffler.exe |
| `-r, --rules` | Path to Snaffler rules file |
| `-t, --threads` | Number of threads (default: 30) |
| `-m, --max-size` | Max file size in KB (default: 500) |
| `--stream` | Stream Snaffler output to console |
| `--timeout` | Timeout in minutes (default: 60) |

### Output Options

| Option | Description |
|--------|-------------|
| `-o, --output-discovery` | Output file for discovered servers |
| `--output-index` | Output file for indexed SCCM files (CSV) |
| `--output-snaffler` | Output file for Snaffler results |
| `--output-report` | Output file for enriched report |

### General Options

| Option | Description |
|--------|-------------|
| `-v, --verbose` | Enable verbose output |

## Output Files

### Discovery Output (`--output-discovery`)
```
sccm01.corp.local
sccm02.corp.local
sccmDP-remote.corp.local
```

### Index Output (`--output-index`)
```csv
INIPath,ContentPath,OriginalFileName,FileSize,Hash,Version
\\sccm01\SCCMContentLib$\DataLib\A1B2\A1B2C3D4.INI,\\sccm01\SCCMContentLib$\DataLib\A1B2\A1B2C3D4,unattend.xml,4523,SHA256_HASH,1.0
```

### Snaffler Output (`--output-snaffler`)
```
2025-10-06 [RED] \\sccm01\SCCMContentLib$\DataLib\A1B2\A1B2C3D4.INI - Unattend_Files - Windows unattended installation file
2025-10-06 [BLACK] \\sccm01\SCCMContentLib$\DataLib\C5D6\C5D6E7F8.INI - Private_Keys - Private key file detected
```

### Enriched Report (`--output-report`)
```
=== SCCM Content Library Analysis Report ===
Generated: 2025-10-06 14:23:45
Total Files Analyzed: 1250
Findings: 37
=============================================

### [BLACK] Severity Findings (5) ###

File: \\sccm01\SCCMContentLib$\DataLib\C5D6\C5D6E7F8.INI
Rule: Private_Keys
Reason: Private key file detected
Original Name: certificate_private.key
File Size: 2,048 bytes
Content Hash: 5F7A8B9C...
Content File: \\sccm01\SCCMContentLib$\DataLib\C5D6\C5D6E7F8
```

## Workflow Examples

### Example 1: Complete SCCM Environment Audit

```bash
# Step 1: Discover all SCCM servers
SnafflerSCCM.exe --domain corp.local \
                 --output-discovery servers.txt \
                 --validate \
                 --verbose

# Step 2: Index all servers and run full Snaffler analysis
SnafflerSCCM.exe --server-list servers.txt \
                 --username sccmaudit \
                 --password SecurePass123 \
                 --snaffler-exe "C:\Tools\Snaffler.exe" \
                 --output-index full_index.csv \
                 --output-snaffler findings.txt \
                 --output-report audit_report.txt \
                 --threads 50 \
                 --verbose
```

### Example 2: Targeted Credential Hunt

```bash
# Focus on credential-related files
SnafflerSCCM.exe --domain corp.local \
                 --extensions xml,config,ini,ps1 \
                 --snaffler-exe "C:\Tools\Snaffler.exe" \
                 --rules credential_rules.toml \
                 --max-size 1024 \
                 --output-report credentials_found.txt
```

### Example 3: Integration with Existing Snaffler Workflow

```bash
# Step 1: Use addon for discovery and indexing
SnafflerSCCM.exe --domain corp.local \
                 --output-index sccm_targets.csv

# Step 2: Use standard Snaffler with custom options
snaffler.exe -f sccm_targets.csv \
             -o custom_output.txt \
             -v debug \
             -r my_rules.toml
```

## Integration with SharpCMLoot

This addon extracts and refactors key components from SharpCMLoot:

- **SCCMShareDiscovery** ← `LdapService.cs`
- **SCCMINIIndexer** ← `InventoryService.cs` + `SmbService.cs`
- **Architecture** ← Service-oriented design pattern

The addon maintains compatibility with SharpCMLoot's discovery methods while adding Snaffler integration.

## SCCM Content Library Structure

Understanding SCCM's storage format:

```
\\server\SCCMContentLib$\
├── DataLib\              # Actual content files
│   ├── A1B2\            # 4-char prefix directory
│   │   ├── A1B2C3D4     # Content file (no extension)
│   │   └── A1B2C3D4.INI # Metadata file
│   └── C5D6\
│       ├── C5D6E7F8
│       └── C5D6E7F8.INI
├── FileLib\              # File references
└── PkgLib\               # Package information
```

**INI File Format:**
```ini
[File]
Name=original_filename.xml
Size=4523
Hash=SHA256_HASH_VALUE
Version=1.0
```

The addon parses these INI files to recover original filenames, making Snaffler analysis more meaningful.

## Performance Considerations

- **Indexing Speed**: ~1000 files/minute (local access), ~500 files/minute (remote SMB)
- **Memory Usage**: ~50MB base + 1MB per 10,000 indexed files
- **Snaffler Performance**: Depends on Snaffler configuration (threads, file size limits)
- **Recommended Settings**:
  - Threads: 30-50 for local networks
  - Max file size: 500KB (INI files are typically small)
  - Timeout: 60+ minutes for large environments

## Troubleshooting

### No SCCM Servers Discovered

```bash
# Try manual server specification
SnafflerSCCM.exe --server sccm01.corp.local --verbose

# Check LDAP connectivity
SnafflerSCCM.exe --domain corp.local --ldap-port 389 --verbose

# Verify credentials have LDAP read access
SnafflerSCCM.exe --domain corp.local --username DOMAIN\user --password pass --verbose
```

### SCCMContentLib$ Access Denied

```bash
# Ensure credentials have read access to administrative shares
net use \\sccm01\SCCMContentLib$ /user:DOMAIN\sccmreader password

# Use explicit authentication
SnafflerSCCM.exe --server sccm01 --username sccmreader --password pass --verbose
```

### Snaffler Execution Fails

```bash
# Verify Snaffler executable exists and runs
"C:\Tools\Snaffler.exe" --help

# Check path in addon
SnafflerSCCM.exe --snaffler-exe "C:\Tools\Snaffler.exe" --verbose

# Run with increased timeout
SnafflerSCCM.exe --snaffler-exe "C:\Tools\Snaffler.exe" --timeout 120
```

## Security Considerations

### Authorized Use Only

This tool is designed for:
- ✅ Authorized security assessments
- ✅ SCCM compliance auditing
- ✅ Incident response investigations
- ✅ Red team operations with written authorization

**Required before use:**
- Written authorization from asset owners
- Defined scope and rules of engagement
- Incident response plan
- Data handling procedures

### Operational Security

- Store credentials securely (use credential managers, not command-line args)
- Encrypt output files containing sensitive findings
- Use secure channels for report transmission
- Implement proper access controls on output directories
- Follow data retention and secure deletion policies

### Detection Considerations

This tool generates:
- LDAP queries (detectable by SIEM)
- SMB connections to SCCM shares (logged in Windows event logs)
- Snaffler execution (may trigger EDR/AV)

For authorized red team operations, coordinate with blue team for appropriate exemptions.

## Future Enhancements

Planned features:
- [ ] Parallel server indexing
- [ ] Resume capability for long-running operations
- [ ] SQLite database for index storage
- [ ] Web-based report viewer
- [ ] Integration with SIEM/SOAR platforms
- [ ] Docker container for cross-platform execution

## Contributing

This is part of the SharpCMLoot ecosystem. Contributions welcome:

1. Fork the repository
2. Create feature branch
3. Implement changes with tests
4. Submit pull request

## License

Same license as SharpCMLoot and Snaffler projects.

## Credits

- **SharpCMLoot**: Discovery and indexing logic
- **Snaffler (ZephrFish fork)**: File analysis engine
- **SMBLibrary**: SMB client implementation
- **CommandLineParser**: Argument parsing

## Author

Created as an integration project combining SharpCMLoot's SCCM discovery with Snaffler's analysis capabilities.

## Support

For issues and questions:
- SharpCMLoot issues: [GitHub Issues](https://github.com/yourusername/SharpCMLoot/issues)
- Snaffler questions: [ZephrFish/Snaffler](https://github.com/ZephrFish/Snaffler)

---

**Version**: 1.0
**Last Updated**: 2025-10-06
