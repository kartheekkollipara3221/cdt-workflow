"""Make one self-extracting PowerShell file with the atlas-platform files for the target (INTEGRATION.md section 1).

Usage (from the repo root): python atlas-platform/local/make-target-package.py <out.ps1>

The package holds only committed files (git ls-files), in the target layout:
  copy/      the files to copy into the target, by the target path (INTEGRATION.md section 1)
  reference/ files to read for the hand edits (INTEGRATION.md section 2); do not copy them
  MANIFEST.txt  one line per file: SHA-256, size, path
The .ps1 runs on Windows PowerShell 5.1. It extracts to a NEW folder only (never into the target repo) and
checks each file against the manifest.
"""
import base64, hashlib, io, subprocess, sys, zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
AP = 'atlas-platform/'

# (source prefix or file in the repo, path in the package)
COPY = [
    ('src/BBH.CP.Atlas.Data/Platform/', 'copy/BBH.CP.Atlas.Data/Platform/'),
    ('src/BBH.CP.Atlas.Service/Platform/', 'copy/BBH.CP.Atlas.Service/Platform/'),
    ('src/BBH.CP.Atlas.Service/Application/ConfigSectionService.cs', 'copy/BBH.CP.Atlas.Service/Application/ConfigSectionService.cs'),
    ('src/BBH.CP.Atlas.App/Platform/', 'copy/BBH.CP.Atlas.App/Platform/'),
    ('src/BBH.CP.Atlas.App/Config/TTDV2.app.json', 'copy/BBH.CP.Atlas.App/Config/TTDV2.app.json'),
    ('src/BBH.CP.Atlas.App/Config/TTDV2.json', 'copy/BBH.CP.Atlas.App/Config/TTDV2.json'),
    ('src/BBH.CP.Atlas.App/Config/TTDV2.Dashboard.json', 'copy/BBH.CP.Atlas.App/Config/TTDV2.Dashboard.json'),
    ('WebUi/src/app/platform/', 'copy/WebUi/src/app/platform/'),
    ('sql/release-p1/', 'copy/sql/release-p1/'),
]
REFERENCE = [
    ('INTEGRATION.md', 'reference/INTEGRATION.md'),
    ('PBDEV.md', 'reference/PBDEV.md'),
    ('src/BBH.CP.Atlas.Service/StandIn/CPAtlasService.cs', 'reference/StandIn/CPAtlasService.cs'),
    ('src/BBH.CP.Atlas.App/StandIn/Controllers/V1/CPAtlasController.cs', 'reference/StandIn/CPAtlasController.cs'),
    ('src/BBH.CP.Atlas.App/StandIn/Program.cs', 'reference/StandIn/Program.cs'),
    ('src/BBH.CP.Atlas.App/Config/README.md', 'reference/Config-README.md'),
    ('WebUi/src/app/app.routes.ts', 'reference/StandIn/app.routes.ts'),
    ('WebUi/src/app/app.config.ts', 'reference/StandIn/app.config.ts'),
]


def git(*args):
    return subprocess.run(['git', *args], cwd=ROOT, check=True, capture_output=True, text=True).stdout


def files():
    tracked = git('ls-files', AP).splitlines()
    out = []
    for source, target in COPY + REFERENCE:
        matched = [f for f in tracked if f == AP + source or (source.endswith('/') and f.startswith(AP + source))]
        if not matched:
            sys.exit(f'No committed file for {source}')
        for f in matched:
            rel = f[len(AP + source):] if source.endswith('/') else ''
            out.append((f, target + rel))
    return out


def main():
    out_path = Path(sys.argv[1])
    dirty = git('status', '--porcelain', '--', AP).strip()
    if dirty:
        sys.exit('atlas-platform has uncommitted changes; commit first so the package matches a commit:\n' + dirty)
    commit = git('rev-parse', '--short', 'HEAD').strip()

    buffer = io.BytesIO()
    manifest = []
    with zipfile.ZipFile(buffer, 'w', zipfile.ZIP_DEFLATED) as z:
        for source, target in files():
            data = (ROOT / source).read_bytes()
            z.writestr(target, data)
            manifest.append(f'{hashlib.sha256(data).hexdigest()}  {len(data):>8}  {target}')
        manifest_text = f'atlas-platform target package, commit {commit}\n' + '\n'.join(manifest) + '\n'
        z.writestr('MANIFEST.txt', manifest_text)
    payload = base64.b64encode(buffer.getvalue()).decode('ascii')
    lines = '\n'.join(payload[i:i + 120] for i in range(0, len(payload), 120))
    hashes = '\n'.join(f'    "{m.split()[2]}" = "{m.split()[0]}"' for m in manifest)

    script = f'''# atlas-platform target package (commit {commit}, {len(manifest)} files).
# Made by atlas-platform/local/make-target-package.py. Windows PowerShell 5.1 or later.
#
# Use:   powershell -ExecutionPolicy Bypass -File .\\{out_path.name} [-Destination C:\\temp\\atlas-{commit}]
# It extracts to a NEW folder only (it stops if the folder exists) and never writes into the target repo.
# Then copy the files under copy\\ into the target as INTEGRATION.md section 1 says; read reference\\ for section 2.
param([string]$Destination = (Join-Path (Get-Location) 'atlas-integration-{commit}'))
$ErrorActionPreference = 'Stop'

if (Test-Path -LiteralPath $Destination) {{ throw "The folder $Destination exists. Give a new folder with -Destination." }}

$expected = @{{
{hashes}
}}

$payload = @'
{lines}
'@

$zipPath = Join-Path ([IO.Path]::GetTempPath()) ('atlas-{commit}-' + [guid]::NewGuid().ToString('N') + '.zip')
try {{
    [IO.File]::WriteAllBytes($zipPath, [Convert]::FromBase64String($payload))
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [IO.Compression.ZipFile]::ExtractToDirectory($zipPath, $Destination)
}}
finally {{
    if (Test-Path -LiteralPath $zipPath) {{ Remove-Item -LiteralPath $zipPath -Force }}
}}

$bad = 0
foreach ($path in $expected.Keys) {{
    $file = Join-Path $Destination ($path -replace '/', '\\')
    if (-not (Test-Path -LiteralPath $file)) {{ Write-Host "MISSING  $path" -ForegroundColor Red; $bad++; continue }}
    $hash = (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($hash -ne $expected[$path]) {{ Write-Host "CHANGED  $path" -ForegroundColor Red; $bad++ }}
}}
if ($bad -gt 0) {{ throw "$bad file(s) do not match the manifest." }}

Write-Host "OK: $($expected.Count) files (commit {commit}) in $Destination"
Write-Host "  copy\\       -> into the target (INTEGRATION.md section 1)"
Write-Host "  reference\\  -> read for the hand edits (INTEGRATION.md section 2); do not copy"
'''
    out_path.write_text(script, encoding='ascii', newline='\r\n')
    print(f'ok {out_path} ({len(manifest)} files, {out_path.stat().st_size // 1024} kB, commit {commit})')


if __name__ == '__main__':
    main()
