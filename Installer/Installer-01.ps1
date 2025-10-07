<#
Run-Installer-With-W.ps1

Flow:
1) Ask user for a folder (GUI).
2) Ensure ...\FChassis exists (create if missing).
3) Map W: to that folder via SUBST (current session).
4) Elevate (UAC) and persist per-user mapping in HKU\<SID>\...\Run.
5) Prompt OK, then run installer.exe next to this script.
#>

[CmdletBinding()]
param(
    [switch]$Elevated,
    [string]$MapPath,
    [string]$OrigSid
)

# Log file path in %LOCALAPPDATA%
$LogFile = Join-Path -Path $env:LOCALAPPDATA -ChildPath "FChassis_Install.log"

# Function to write to log file with timestamp
function Write-Log {
    param(
        [string]$Message,
        [string]$Level = "INFO"
    )
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $logMessage = "[$timestamp] [$Level] $Message"
    Add-Content -Path $LogFile -Value $logMessage -Force
    Write-Host $logMessage
}

# Ensure log file exists
if (-not (Test-Path -LiteralPath $LogFile)) {
    Write-Log -Message "Creating log file at: $LogFile"
    New-Item -ItemType File -Path $LogFile -Force | Out-Null
}
Write-Log -Message "=== Starting Run-Installer-With-W.ps1 ==="

function Get-OriginalUserSid {
    Write-Log -Message "Retrieving SID of the current user..."
    $sid = ([System.Security.Principal.WindowsIdentity]::GetCurrent().User.Value)
    Write-Log -Message "Retrieved SID: $sid"
    return $sid
}

function Ensure-FolderPicker {
    Write-Log -Message "Prompting user for folder selection..."
    try {
        Add-Type -AssemblyName System.Windows.Forms -ErrorAction Stop | Out-Null
        if ([Threading.Thread]::CurrentThread.ApartmentState -ne 'STA') {
            Write-Log -Message "Current thread is not STA. Re-running script in STA mode."
            $argsList = @()
            powershell -NoProfile -STA -ExecutionPolicy Bypass -File $PSCommandPath @args
            $exitCode = $LASTEXITCODE
            Write-Log -Message "STA mode execution completed with exit code: $exitCode"
            exit $exitCode
        }
        $dlg = New-Object System.Windows.Forms.FolderBrowserDialog
        $dlg.Description = "Select the folder for drive FChassis data (it will be mapped to W:)"
        $dlg.ShowNewFolderButton = $true
        if ($dlg.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK) {
            Write-Log -Message "User selected folder: $($dlg.SelectedPath)"
            return $dlg.SelectedPath
        } else {
            Write-Log -Message "User cancelled folder selection." -Level "WARNING"
            return $null
        }
    } catch {
        Write-Log -Message "Failed to use WinForms. Falling back to Shell.Application dialog: $($_.Exception.Message)" -Level "ERROR"
        $sh = New-Object -ComObject Shell.Application
        $f = $sh.BrowseForFolder(0, 'Select the folder for drive FChassis data (it will be mapped to W:)', 0)
        if ($f) {
            $selectedPath = $f.Self.Path
            Write-Log -Message "User selected folder via fallback: $selectedPath"
            return $selectedPath
        } else {
            Write-Log -Message "User cancelled folder selection in fallback dialog." -Level "WARNING"
            return $null
        }
    }
}

function Ensure-FChassisSubfolder([string]$basePath) {
    Write-Log -Message "Ensuring FChassis subfolder exists for path: $basePath"
    if ([string]::IsNullOrWhiteSpace($basePath)) {
        Write-Log -Message "Base path is empty or invalid." -Level "ERROR"
        return $null
    }
    # Trim trailing backslash
    if ($basePath.EndsWith('\')) {
        $basePath = $basePath.TrimEnd('\')
        Write-Log -Message "Trimmed trailing backslash from path: $basePath"
    }
    $lastSeg = Split-Path $basePath -Leaf
    if ($lastSeg -ieq 'FChassis') {
        $target = $basePath
    } else {
        $target = Join-Path $basePath 'FChassis'
    }
    if (-not (Test-Path -LiteralPath $target)) {
        Write-Log -Message "Creating FChassis directory: $target"
        New-Item -ItemType Directory -Path $target -Force | Out-Null
    } else {
        Write-Log -Message "FChassis directory already exists: $target"
    }
    Write-Log -Message "FChassis subfolder ensured: $target"
    return $target
}

function Map-DriveW-Now([string]$path) {
    Write-Log -Message "Mapping W: drive to path: $path"
    # Unmap any existing W:
    Write-Log -Message "Removing any existing W: drive mapping..."
    cmd /c "subst W: /d" | Out-Null
    if ($LASTEXITCODE -eq 0) {
        Write-Log -Message "Successfully removed existing W: drive mapping."
    } else {
        Write-Log -Message "No existing W: drive mapping found or removal failed (exit code: $LASTEXITCODE)." -Level "WARNING"
    }
    # Map W: to path
    Write-Log -Message "Executing subst command to map W: to $path"
    $null = cmd /c "subst W: `"$path`""
    if ($LASTEXITCODE -ne 0) {
        Write-Log -Message "Failed to map W: to $path (exit code: $LASTEXITCODE)." -Level "ERROR"
        throw "Failed to map W: to `"$path`" (exit code $LASTEXITCODE)."
    }
    Write-Log -Message "Successfully mapped W: to $path"
}

function Persist-PerUser-RunKey([string]$sid, [string]$path) {
    Write-Log -Message "Persisting W: drive mapping for user SID: $sid to path: $path"
    $runKey = "Registry::HKEY_USERS\$sid\Software\Microsoft\Windows\CurrentVersion\Run"
    Write-Log -Message "Ensuring registry key exists: $runKey"
    New-Item -Path $runKey -Force | Out-Null
    Write-Log -Message "Setting registry value MapW in $runKey"
    New-ItemProperty -Path $runKey -Name 'MapW' -PropertyType ExpandString -Value "cmd.exe /c subst W: `"$path`"" -Force | Out-Null
    Write-Log -Message "Successfully set registry value MapW for persistent W: mapping"
    return $runKey
}

function Show-InfoBox([string]$text, [string]$title) {
    Write-Log -Message "Displaying info box: $title"
    Add-Type -AssemblyName System.Windows.Forms | Out-Null
    [System.Windows.Forms.MessageBox]::Show($text, $title, 'OK', 'Information') | Out-Null
    Write-Log -Message "Info box displayed and closed"
}

# --------------------------
# MAIN
# --------------------------

if (-not $Elevated) {
    # 1) Folder UI
    Write-Log -Message "Starting non-elevated branch: Prompting for folder selection"
    $picked = Ensure-FolderPicker
    if (-not $picked) {
        Write-Log -Message "No folder selected. Exiting script." -Level "ERROR"
        exit 1
    }

    # 2) Ensure ...\FChassis exists
    Write-Log -Message "Ensuring FChassis subfolder for selected path: $picked"
    $finalPath = Ensure-FChassisSubfolder -basePath $picked
    if (-not $finalPath) {
        Write-Log -Message "Invalid path selected. Exiting script." -Level "ERROR"
        exit 1
    }

    # 3) Map W: now (session)
    try {
        Map-DriveW-Now -path $finalPath
        Write-Log -Message "Successfully mapped W: to $finalPath in current session"
    } catch {
        Write-Log -Message "Failed to map W: drive: $($_.Exception.Message)" -Level "ERROR"
        exit 2
    }

    # Original (current) user SID to persist under HKU\<SID>
    Write-Log -Message "Retrieving original user SID for registry persistence"
    $sid = Get-OriginalUserSid

    # 4) Elevate and pass args
    Write-Log -Message "Initiating UAC elevation for registry persistence"
    $argsList = @(
        '-NoProfile', '-ExecutionPolicy', 'Bypass',
        '-File', ('"{0}"' -f $PSCommandPath),
        '-Elevated',
        '-MapPath', ('"{0}"' -f $finalPath),
        '-OrigSid', ('"{0}"' -f $sid)
    )
    try {
        Start-Process -FilePath 'powershell.exe' -ArgumentList $argsList -Verb RunAs | Out-Null
        Write-Log -Message "Successfully initiated elevated process"
    } catch {
        Write-Log -Message "Elevation cancelled or failed: $($_.Exception.Message). W: mapped for this session only." -Level "ERROR"
        exit 3
    }
    Write-Log -Message "Non-elevated branch completed successfully"
    exit 0
}

# ---- Elevated branch ----
Write-Log -Message "Starting elevated branch"
if ([string]::IsNullOrWhiteSpace($MapPath)) {
    Write-Log -Message "Missing MapPath parameter; cannot proceed." -Level "ERROR"
    exit 10
}
if ([string]::IsNullOrWhiteSpace($OrigSid)) {
    Write-Log -Message "Missing OrigSid parameter; cannot proceed." -Level "ERROR"
    exit 11
}

# 4) Persist W: mapping in registry
try {
    $key = Persist-PerUser-RunKey -sid $OrigSid -path $MapPath
    Write-Log -Message "Successfully persisted W: mapping in registry: $key"
} catch {
    Write-Log -Message "Failed to persist W: mapping in registry: $($_.Exception.Message)" -Level "ERROR"
    Write-Log -Message "Manual persistence command: cmd /c subst W: `"$MapPath`""
}

# 5) Prompt and run installer.exe
Write-Log -Message "Preparing to launch installer.exe"
Show-InfoBox -text ("Drive W: is set to:`n{0}`n`nClick OK to start the installer." -f $MapPath) -title 'Ready to Install'

$installer = Join-Path -Path $PSScriptRoot -ChildPath 'installer.exe'
Write-Log -Message "Checking for installer at: $installer"
if (-not (Test-Path -LiteralPath $installer)) {
    Write-Log -Message "Cannot find installer.exe at: $installer" -Level "ERROR"
    exit 20
}

Write-Log -Message "Launching installer: $installer"
Start-Process -FilePath $installer | Out-Null
Write-Log -Message "Installer launched successfully"
Write-Log -Message "=== Run-Installer-With-W.ps1 completed ==="
exit 0