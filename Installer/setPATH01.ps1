<#
PowerShell script to append folder paths from a file to the system PATH variable
Usage: .\setPATH01.ps1 -FilePath "C:\paths.txt"
#>

# Parameter definition (must be the first executable statement)
param(
    [Parameter(Mandatory=$true)]
    [string]$FilePath
)

function Pause-Script {
    Write-Host "Press any key to continue..."
    $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
}

# Get the script directory for relative path resolution
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition

Write-Host "Script started at $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
Write-Host "Script directory: $scriptDir"
Write-Host "Parameter received: FilePath = $FilePath"

# Function to check if running as Administrator
function Test-Administrator {
    $currentUser = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
    return $currentUser.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

# Resolve file path (handle relative paths)
function Resolve-FilePath {
    param([string]$Path)
    
    if ([System.IO.Path]::IsPathRooted($Path)) {
        return $Path
    } else {
        return (Join-Path $scriptDir $Path)
    }
}

$resolvedFilePath = Resolve-FilePath -Path $FilePath
Write-Host "Resolved file path: $resolvedFilePath"

# If not running as admin, elevate using UAC
if (-not (Test-Administrator)) {
    Write-Host "Not running as administrator, elevating privileges..."
    
    try {
        # Start elevated process with -NoExit to keep window open
        # Pass the resolved absolute path to the elevated process
        $arguments = "-NoExit -NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`" -FilePath `"$resolvedFilePath`""
        Start-Process powershell.exe -Verb RunAs -ArgumentList $arguments
        Write-Host "Elevated process launched successfully."
    } catch {
        Write-Host "Error elevating privileges: $_"
        Pause-Script
        exit 1
    }
    exit
} else {
    Write-Host "Running with administrator privileges."
}

# Validate file exists
if (-not (Test-Path -Path $resolvedFilePath -PathType Leaf)) {
    Write-Host "Error: File '$resolvedFilePath' not found or is not a valid file"
    Write-Host "Current working directory: $(Get-Location)"
    Pause-Script
    exit 1
}

# Read the file content
try {
    $newPaths = Get-Content -Path $resolvedFilePath -ErrorAction Stop
    Write-Host "Successfully read $($newPaths.Count) paths from file"
} catch {
    Write-Host "Error reading file: $_"
    Pause-Script
    exit 1
}

# Get current system PATH
try {
    $currentPath = [Environment]::GetEnvironmentVariable("Path", "Machine")
    Write-Host "Current system PATH retrieved: Contains $(($currentPath -split ';').Count) entries"
} catch {
    Write-Host "Error retrieving system PATH: $_"
    Pause-Script
    exit 1
}

# Append each path if not already present
$updatedPath = $currentPath
$pathsAdded = 0

foreach ($path in $newPaths) {
    $trimmedPath = $path.Trim()
    
    if (-not [string]::IsNullOrEmpty($trimmedPath)) {
        if (-not (Test-Path -Path $trimmedPath -PathType Container)) {
            Write-Host "Warning: Skipping invalid path: $trimmedPath"
            continue
        }
        
        # More precise check for path existence
        $pathPattern = [regex]::Escape($trimmedPath)
        if (-not ($currentPath -match "(^|;)$pathPattern($|;)")) {
            $updatedPath += ";$trimmedPath"
            $pathsAdded++
            Write-Host "Added path: $trimmedPath"
        } else {
            Write-Host "Path already exists: $trimmedPath"
        }
    }
}

# Set the updated PATH if changes were made
if ($pathsAdded -gt 0) {
    try {
        [Environment]::SetEnvironmentVariable("Path", $updatedPath, "Machine")
        Write-Host "System PATH updated successfully with $pathsAdded new paths"
        Write-Host "Total entries in PATH: $(($updatedPath -split ';').Count)"
        
        # Also update the current session's PATH
        $env:Path = [Environment]::GetEnvironmentVariable("Path", "Machine") + ";" + [Environment]::GetEnvironmentVariable("Path", "User")
    } catch {
        Write-Host "Error updating system PATH: $_"
        Pause-Script
        exit 1
    }
} else {
    Write-Host "No changes made to system PATH - all paths already exist"
}

Write-Host "Script completed at $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
Pause-Script