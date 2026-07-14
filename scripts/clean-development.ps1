[CmdletBinding()]
param(
    [switch]$Apply,
    [switch]$Deep,
    [switch]$KeepBuilds
)

$ErrorActionPreference = 'Stop'
$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$UnityRoot = Join-Path $RepoRoot 'src\AstilCodex.UnityClient'

function Format-ByteSize {
    param([long]$Bytes)

    if ($Bytes -ge 1TB) { return '{0:N2} TB' -f ($Bytes / 1TB) }
    if ($Bytes -ge 1GB) { return '{0:N2} GB' -f ($Bytes / 1GB) }
    if ($Bytes -ge 1MB) { return '{0:N2} MB' -f ($Bytes / 1MB) }
    if ($Bytes -ge 1KB) { return '{0:N2} KB' -f ($Bytes / 1KB) }
    return "$Bytes B"
}

function Get-PathSize {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) { return 0L }
    $item = Get-Item -LiteralPath $Path -Force
    if (-not $item.PSIsContainer) { return [long]$item.Length }

    $sum = Get-ChildItem -LiteralPath $Path -File -Recurse -Force -ErrorAction SilentlyContinue |
        Measure-Object -Property Length -Sum
    return [long]($sum.Sum)
}

function Add-Candidate {
    param(
        [System.Collections.Generic.List[string]]$List,
        [string]$Path
    )

    if ([string]::IsNullOrWhiteSpace($Path)) { return }
    $fullPath = [System.IO.Path]::GetFullPath($Path)
    if (-not $fullPath.StartsWith($RepoRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to clean outside the repository: $fullPath"
    }

    if ((Test-Path -LiteralPath $fullPath) -and -not $List.Contains($fullPath)) {
        $List.Add($fullPath)
    }
}

$candidates = New-Object 'System.Collections.Generic.List[string]'

# .NET output folders are discovered from project files so unrelated folders are untouched.
Get-ChildItem -Path $RepoRoot -Filter '*.csproj' -File -Recurse -ErrorAction SilentlyContinue |
    ForEach-Object {
        Add-Candidate $candidates (Join-Path $_.Directory.FullName 'bin')
        Add-Candidate $candidates (Join-Path $_.Directory.FullName 'obj')
    }

# Unity output that is safe to regenerate.
$unityGenerated = @('Temp', 'Obj', 'Logs', 'MemoryCaptures', 'Recordings')
foreach ($name in $unityGenerated) {
    Add-Candidate $candidates (Join-Path $UnityRoot $name)
}

if (-not $KeepBuilds) {
    Add-Candidate $candidates (Join-Path $UnityRoot 'Build')
    Add-Candidate $candidates (Join-Path $UnityRoot 'Builds')
}

# Python caches in the reference simulator, repository scripts, and validators.
$pythonRoots = @('prototypes', 'scripts', 'src\AstilCodex.UnityClient')
foreach ($relativeRoot in $pythonRoots) {
    $pythonRoot = Join-Path $RepoRoot $relativeRoot
    if (Test-Path -LiteralPath $pythonRoot) {
        Get-ChildItem -Path $pythonRoot -Directory -Filter '__pycache__' -Recurse -ErrorAction SilentlyContinue |
            ForEach-Object { Add-Candidate $candidates $_.FullName }
    }
}
Add-Candidate $candidates (Join-Path $RepoRoot '.pytest_cache')
Add-Candidate $candidates (Join-Path $RepoRoot '.mypy_cache')
Add-Candidate $candidates (Join-Path $RepoRoot '.ruff_cache')

if ($Deep) {
    # Library is usually the largest Unity folder. Deleting it is safe but forces a full package
    # resolution, shader import, and asset reimport the next time the Editor opens.
    Add-Candidate $candidates (Join-Path $UnityRoot 'Library')
    Add-Candidate $candidates (Join-Path $UnityRoot 'UserSettings')
}

Write-Host "Astil Codex development cleanup" -ForegroundColor Cyan
Write-Host "Repository: $RepoRoot"
Write-Host "Mode: $(if ($Apply) { 'APPLY' } else { 'DRY RUN' })$(if ($Deep) { ' + DEEP' } else { '' })"
Write-Host ''

$total = 0L
$items = @()
foreach ($path in $candidates | Sort-Object) {
    $size = Get-PathSize $path
    $total += $size
    $items += [PSCustomObject]@{
        Size = Format-ByteSize $size
        Path = $path.Substring($RepoRoot.Length).TrimStart('\', '/')
        FullPath = $path
    }
}

if ($items.Count -eq 0) {
    Write-Host 'No generated development files were found.' -ForegroundColor Green
    exit 0
}

$items | Select-Object Size, Path | Format-Table -AutoSize
Write-Host "Potential space recovery: $(Format-ByteSize $total)" -ForegroundColor Yellow

if (-not $Apply) {
    Write-Host ''
    Write-Host 'Dry run only. Add -Apply to delete the listed paths.' -ForegroundColor Cyan
    if (-not $Deep) {
        Write-Host 'Add -Deep to include Unity Library and UserSettings (full reimport required).' -ForegroundColor DarkYellow
    }
    exit 0
}

$blockingNames = @('Unity', 'AstilCodex', 'astil-core-host')
$blocking = Get-Process -ErrorAction SilentlyContinue |
    Where-Object { $blockingNames -contains $_.ProcessName }
if ($blocking) {
    $names = ($blocking | Select-Object -ExpandProperty ProcessName -Unique) -join ', '
    throw "Close these processes before cleanup: $names"
}

foreach ($item in $items) {
    try {
        Remove-Item -LiteralPath $item.FullPath -Recurse -Force -ErrorAction Stop
        Write-Host "Removed: $($item.Path)" -ForegroundColor Green
    }
    catch {
        Write-Warning "Could not remove $($item.Path): $($_.Exception.Message)"
    }
}

Write-Host ''
Write-Host "Cleanup complete. Planned recovery was $(Format-ByteSize $total)." -ForegroundColor Green
Write-Host 'Source files, Git history, ProjectSettings, Packages, local conversation memory, and avatars were not touched.'
