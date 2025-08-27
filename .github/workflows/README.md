# GitHub Actions Workflows

## Single Consolidated Workflow: `build.yml`

This project uses a single, efficient workflow that handles all CI/CD needs:

### Features
- **Automatic builds** on push to main/master/develop branches
- **Pull request validation** with automatic testing
- **Manual releases** via workflow dispatch
- **Automatic versioning** using semantic versioning
- **Artifact generation** for both executables and ZIP packages

### Triggers

| Trigger | Action | Release |
|---------|--------|---------|
| Push to main | Build + Test | Auto-patch release if code changed |
| Push to develop | Build + Test | No release |
| Pull Request | Build + Test | No release |
| Manual (patch) | Build + Test | Patch release (x.x.+1) |
| Manual (minor) | Build + Test | Minor release (x.+1.0) |
| Manual (major) | Build + Test | Major release (+1.0.0) |

### Outputs

Every successful build produces:
- `SCML_Standalone.exe` - Main tool with embedded dependencies
- `MockSCCMServer_Standalone.exe` - Test server
- `SCML_vX.X.X.zip` - Full package with documentation
- `MockServer_vX.X.X.zip` - Mock server package
- `SCML_Complete_vX.X.X.zip` - All-in-one package

### Manual Release

To create a release manually:
1. Go to Actions tab
2. Select "Build & Release" workflow
3. Click "Run workflow"
4. Choose release type (patch/minor/major)
5. Click "Run workflow"

The workflow will automatically:
- Increment the version number
- Build and test the code
- Create packages
- Publish a GitHub release