# Snaffler SCCM Addon - Quick Start Guide

## 60-Second Setup

```bash
# 1. Build the addon
cd SnafflerSCCMAddon
msbuild SnafflerSCCMAddon.csproj /p:Configuration=Release

# 2. Run against your domain
bin\Release\SnafflerSCCM.exe --domain corp.local \
                             --snaffler-exe "C:\Tools\Snaffler.exe" \
                             --output-report results.txt

# 3. Review findings
notepad results.txt
```

## Common Usage Patterns

### 1. Quick Domain Scan
```bash
SnafflerSCCM.exe -d corp.local --snaffler-exe Snaffler.exe
```

### 2. Authenticated Scan
```bash
SnafflerSCCM.exe -d corp.local -u sccmreader -p password --snaffler-exe Snaffler.exe
```

### 3. Target Specific Server
```bash
SnafflerSCCM.exe -s sccm01.corp.local --snaffler-exe Snaffler.exe -o findings.txt
```

### 4. Credential Hunt (High Value Files Only)
```bash
SnafflerSCCM.exe -d corp.local \
                 -e xml,config,ini,ps1,key,pfx \
                 --snaffler-exe Snaffler.exe \
                 -r credential_rules.toml \
                 --output-report critical_findings.txt
```

### 5. Discovery Only (No Snaffler)
```bash
SnafflerSCCM.exe -d corp.local -o servers.txt --output-index index.csv
```

## Output Files Explained

After running, you'll get:

1. **Console Output**: Real-time progress and high-value findings
2. **Snaffler Results** (`--output-snaffler`): Raw Snaffler output
3. **Enriched Report** (`--output-report`): Combined SCCM + Snaffler data
4. **Index File** (`--output-index`): CSV of all indexed files

## What Gets Analyzed?

The addon:
1. Discovers SCCM Distribution Points via LDAP
2. Indexes `\\server\SCCMContentLib$\DataLib\**\*.INI` files
3. Parses INI files to recover original filenames
4. Runs Snaffler against the indexed files
5. Enriches findings with SCCM metadata

## Performance Tips

- **Small environment** (<5 servers): Default settings work fine
- **Medium environment** (5-20 servers): Increase threads to 50
- **Large environment** (20+ servers): Run per-server and aggregate results

```bash
# Optimized for large environments
SnafflerSCCM.exe -d corp.local \
                 --snaffler-exe Snaffler.exe \
                 -t 50 \
                 -m 1024 \
                 --timeout 120 \
                 -v
```

## Troubleshooting

### "No SCCM servers discovered"
→ Try: `SnafflerSCCM.exe -s KNOWN_SCCM_SERVER --validate -v`

### "Access Denied to SCCMContentLib$"
→ Use credentials: `SnafflerSCCM.exe -u DOMAIN\admin -p password`

### "Snaffler execution failed"
→ Verify path: `SnafflerSCCM.exe --snaffler-exe "C:\Full\Path\To\Snaffler.exe" -v`

## Real-World Example

```bash
# Compliance audit scenario
SnafflerSCCM.exe --domain contoso.local \
                 --username svc_sccm_audit \
                 --password CompliancePass123 \
                 --snaffler-exe "C:\Tools\Snaffler.exe" \
                 --rules sccm_compliance_rules.toml \
                 --extensions xml,config,ini,ps1 \
                 --output-discovery discovered_sccm.txt \
                 --output-index full_index.csv \
                 --output-report compliance_audit_2025.txt \
                 --threads 40 \
                 --verbose
```

**Expected output:**
- Discovery: ~30 seconds
- Indexing: ~2-5 minutes per server
- Snaffler analysis: ~10-30 minutes (depends on file count)
- Total: 15-60 minutes for typical environment

## Next Steps

1. Review enriched report for BLACK/RED findings
2. Investigate flagged files in SCCM console
3. Remediate misconfigurations
4. Schedule periodic audits

## Need Help?

- Full documentation: See `README.md`
- Examples: See `README.md` → "Workflow Examples"
- Issues: GitHub Issues page
