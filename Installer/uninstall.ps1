# uninstallFChassis.ps1
# Complete FChassis Uninstaller Script
# Run this script as Administrator to completely remove FChassis

param(
    [switch]$Force = $false,
    [switch]$KeepThirdParty = $false,
    [string]$InstallDir = "C:\FChassis"
)

$APPNAME = "FChassis"
$COMPANY = "Teckinsoft Neuronics Pvt. Ltd."
$UNINSTALL_KEY = "Software\Microsoft\Windows\CurrentVersion\Uninstall\$APPNAME"
$INSTALL_FLAG_KEY = "Software\$COMPANY\$APPNAME"

function Write-ColorOutput($ForegroundColor) {
    $fc = $host.UI.RawUI.ForegroundColor
    $host.UI.RawUI.ForegroundColor = $ForegroundColor
    if ($args) {
        Write-Output $args
    }
    $host.UI.RawUI.ForegroundColor = $fc
}

function Test-Admin {
    $currentUser = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
    return $currentUser.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Remove-RegistryKey($Path, $IsHKCU = $false) {
    $root = if ($IsHKCU) { "HKCU:" } else { "HKLM:" }
    $fullPath = "$root\$Path"
    
    if (Test-Path $fullPath) {
        try {
            Remove-Item -Path $fullPath -Recurse -Force -ErrorAction Stop
            Write-ColorOutput Green "✓ Removed registry key: $Path"
            return $true
        }
        catch {
            Write-ColorOutput Red "✗ Failed to remove registry key: $Path"
            Write-ColorOutput Red "  Error: $($_.Exception.Message)"
            return $false
        }
    }
    else {
        Write-ColorOutput Yellow "ℹ Registry key not found: $Path"
        return $true
    }
}

function Remove-RegistryValue($Path, $ValueName, $IsHKCU = $false) {
    $root = if ($IsHKCU) { "HKCU:" } else { "HKLM:" }
    $fullPath = "$root\$Path"
    
    if (Test-Path $fullPath) {
        try {
            Remove-ItemProperty -Path $fullPath -Name $ValueName -Force -ErrorAction Stop
            Write-ColorOutput Green "✓ Removed registry value: $Path\$ValueName"
            return $true
        }
        catch {
            Write-ColorOutput Red "✗ Failed to remove registry value: $Path\$ValueName"
            Write-ColorOutput Red "  Error: $($_.Exception.Message)"
            return $false
        }
    }
    else {
        Write-ColorOutput Yellow "ℹ Registry path not found: $Path"
        return $true
    }
}

function Remove-FromPath($PathToRemove, $IsHKCU = $false) {
    $root = if ($IsHKCU) { "HKCU:" } else { "HKLM:" }
    $envPath = "$root\SYSTEM\CurrentControlSet\Control\Session Manager\Environment"
    
    if (Test-Path $envPath) {
        try {
            $currentPath = (Get-ItemProperty -Path $envPath -Name "Path" -ErrorAction Stop).Path
            if ($currentPath -like "*$PathToRemove*") {
                $newPath = ($currentPath -split ';' | Where-Object { $_ -ne $PathToRemove }) -join ';'
                Set-ItemProperty -Path $envPath -Name "Path" -Value $newPath -ErrorAction Stop
                Write-ColorOutput Green "✓ Removed from PATH: $PathToRemove"
                return $true
            }
            else {
                Write-ColorOutput Yellow "ℹ Path not found in PATH variable: $PathToRemove"
                return $true
            }
        }
        catch {
            Write-ColorOutput Red "✗ Failed to remove from PATH: $PathToRemove"
            Write-ColorOutput Red "  Error: $($_.Exception.Message)"
            return $false
        }
    }
    else {
        Write-ColorOutput Yellow "ℹ Environment path not found"
        return $true
    }
}

function Remove-VirtualDrive {
    try {
        # Remove W: drive mapping
        $null = subst W: /D 2>$null
        Write-ColorOutput Green "✓ Removed virtual drive W:"
        return $true
    }
    catch {
        Write-ColorOutput Yellow "ℹ Virtual drive W: not found or already removed"
        return $true
    }
}

function Broadcast-EnvironmentChange {
    try {
        # Notify system of environment changes
        $null = [System.Environment]::SetEnvironmentVariable("TEMP", [System.Environment]::GetEnvironmentVariable("TEMP"), "Machine")
        Write-ColorOutput Green "✓ Broadcasted environment changes"
        return $true
    }
    catch {
        Write-ColorOutput Red "✗ Failed to broadcast environment changes"
        Write-ColorOutput Red "  Error: $($_.Exception.Message)"
        return $false
    }
}

function Remove-FilesAndFolders($Path) {
    if (Test-Path $Path) {
        try {
            Write-ColorOutput Cyan "Deleting: $Path"
            if (Test-Path $Path -PathType Container) {
                # Use robocopy for faster multithreaded deletion
                $emptyDir = Join-Path $env:TEMP "empty"
                New-Item -ItemType Directory -Path $emptyDir -Force | Out-Null
                
                $result = Start-Process -FilePath "robocopy" -ArgumentList "`"$emptyDir`" `"$Path`" /purge /MT:8 /NP /NFL /NDL" -Wait -PassThru -WindowStyle Hidden
                
                Remove-Item $emptyDir -Force -Recurse -ErrorAction SilentlyContinue
                
                if ($result.ExitCode -ge 8) {
                    # Fallback to standard deletion if robocopy fails
                    Remove-Item $Path -Force -Recurse -ErrorAction Stop
                }
            } else {
                Remove-Item $Path -Force -ErrorAction Stop
            }
            Write-ColorOutput Green "✓ Successfully deleted: $Path"
            return $true
        }
        catch {
            Write-ColorOutput Red "✗ Failed to delete: $Path"
            Write-ColorOutput Red "  Error: $($_.Exception.Message)"
            return $false
        }
    }
    else {
        Write-ColorOutput Yellow "ℹ Path not found: $Path"
        return $true
    }
}

function Remove-Shortcuts {
    $success = $true
    
    # Remove Start Menu shortcut
    $startMenuPath = Join-Path $env:ProgramData "Microsoft\Windows\Start Menu\Programs\$APPNAME"
    if (Test-Path "$startMenuPath\$APPNAME.lnk") {
        try {
            Remove-Item "$startMenuPath\$APPNAME.lnk" -Force -ErrorAction Stop
            Write-ColorOutput Green "✓ Removed Start Menu shortcut"
        }
        catch {
            Write-ColorOutput Red "✗ Failed to remove Start Menu shortcut"
            $success = $false
        }
    }
    
    # Remove Start Menu folder if empty
    if (Test-Path $startMenuPath) {
        try {
            if ((Get-ChildItem $startMenuPath -Force | Measure-Object).Count -eq 0) {
                Remove-Item $startMenuPath -Force -ErrorAction Stop
            }
        }
        catch {
            # Ignore errors for folder removal
        }
    }
    
    # Remove Desktop shortcut
    $desktopPath = [Environment]::GetFolderPath("Desktop")
    if (Test-Path "$desktopPath\$APPNAME.lnk") {
        try {
            Remove-Item "$desktopPath\$APPNAME.lnk" -Force -ErrorAction Stop
            Write-ColorOutput Green "✓ Removed Desktop shortcut"
        }
        catch {
            Write-ColorOutput Red "✗ Failed to remove Desktop shortcut"
            $success = $false
        }
    }
    
    return $success
}

function Main {
    Write-ColorOutput Cyan "=============================================="
    Write-ColorOutput Cyan "FChassis Complete Uninstaller"
    Write-ColorOutput Cyan "=============================================="
    Write-Output ""

    # Check if running as administrator
    if (-not (Test-Admin)) {
        Write-ColorOutput Red "This script must be run as Administrator!"
        Write-Output "Please right-click on PowerShell and select 'Run as Administrator'"
        pause
        exit 1
    }

    if (-not $Force) {
        Write-ColorOutput Yellow "WARNING: This script will completely remove FChassis from your system."
        Write-ColorOutput Yellow "This includes:"
        Write-ColorOutput Yellow "  - All application files in $InstallDir"
        Write-ColorOutput Yellow "  - Third-party components in parallel directory"
        Write-ColorOutput Yellow "  - Registry entries"
        Write-ColorOutput Yellow "  - Environment variables"
        Write-ColorOutput Yellow "  - Shortcuts"
        $confirm = Read-Host "Do you want to continue? (y/N)"
        if ($confirm -notmatch "^[Yy]$") {
            Write-Output "Operation cancelled."
            exit 0
        }
    }

    Write-Output ""
    Write-ColorOutput Cyan "Starting complete uninstallation..."
    Write-Output ""

    $successCount = 0
    $totalOperations = 0

    # Get parent directory for parallel thirdparty directory
    $parentDir = Split-Path $InstallDir -Parent
    $thirdPartyDir = Join-Path $parentDir "thirdParty"

    # Remove shortcuts
    $totalOperations++
    if (Remove-Shortcuts) { $successCount++ }

    # Remove installed files
    $totalOperations++
    if (Remove-FilesAndFolders $InstallDir) { $successCount++ }

    # Remove parallel thirdparty directory (unless KeepThirdParty flag is set)
    if (-not $KeepThirdParty) {
        $totalOperations++
        if (Remove-FilesAndFolders $thirdPartyDir) { $successCount++ }
    }
    else {
        Write-ColorOutput Yellow "ℹ Keeping third-party directory: $thirdPartyDir"
        $successCount++
    }

    # Remove uninstall registry entries
    $totalOperations++
    if (Remove-RegistryKey $UNINSTALL_KEY) { $successCount++ }

    $totalOperations++
    if (Remove-RegistryKey $UNINSTALL_KEY $true) { $successCount++ } # HKCU

    # Remove installation flag registry entries
    $totalOperations++
    if (Remove-RegistryKey $INSTALL_FLAG_KEY) { $successCount++ }

    $totalOperations++
    if (Remove-RegistryKey $INSTALL_FLAG_KEY $true) { $successCount++ } # HKCU

    # Remove CASROOT environment variable
    $totalOperations++
    if (Remove-RegistryValue "SYSTEM\CurrentControlSet\Control\Session Manager\Environment" "CASROOT") { $successCount++ }

    $totalOperations++
    if (Remove-RegistryValue "Environment" "CASROOT" $true) { $successCount++ } # HKCU

    # Remove virtual drive registry entry
    $totalOperations++
    if (Remove-RegistryValue "Software\Microsoft\Windows\CurrentVersion\Run" "MyVirtualDrive" $true) { $successCount++ }

    # Remove from PATH environment variable
    $pathsToRemove = @(
        "\thirdParty\OpenCASCADE-7.7.0-vc14-64\draco-1.4.1-vc14-64\bin",
        "\thirdParty\OpenCASCADE-7.7.0-vc14-64\opencascade-7.7.0\win64\vc14\bin",
        "\thirdParty\OpenCASCADE-7.7.0-vc14-64\ffmpeg-3.3.4-64\bin",
        "\thirdParty\OpenCASCADE-7.7.0-vc14-64\freeimage-3.17.0-vc14-64\bin",
        "\thirdParty\OpenCASCADE-7.7.0-vc14-64\freetype-2.5.5-vc14-64\bin",
        "\thirdParty\OpenCASCADE-7.7.0-vc14-64\openvr-1.14.15-64\bin\win64",
        "\thirdParty\OpenCASCADE-7.7.0-vc14-64\qt5.11.2-vc14-64\bin",
        "\thirdParty\OpenCASCADE-7.7.0-vc14-64\rapidjson-1.1.0\bin",
        "\thirdParty\OpenCASCADE-7.7.0-vc14-64\tbb_2021.5-vc14-64\bin",
        "\thirdParty\OpenCASCADE-7.7.0-vc14-64\tcltk-86-64\bin",
        "\thirdParty\OpenCASCADE-7.7.0-vc14-64\vtk-6.1.0-vc14-64\bin"
    )

    foreach ($path in $pathsToRemove) {
        $fullPath = Join-Path $thirdPartyDir $path
        $totalOperations++
        if (Remove-FromPath $fullPath) { $successCount++ }
    }

    # Remove virtual drive mapping
    $totalOperations++
    if (Remove-VirtualDrive) { $successCount++ }

    # Broadcast environment changes
    $totalOperations++
    if (Broadcast-EnvironmentChange) { $successCount++ }

    Write-Output ""
    Write-ColorOutput Cyan "=============================================="
    Write-ColorOutput Cyan "Uninstallation Summary"
    Write-ColorOutput Cyan "=============================================="
    Write-Output "Operations attempted: $totalOperations"
    Write-Output "Operations successful: $successCount"
    Write-Output "Operations failed: $($totalOperations - $successCount)"
    
    if ($successCount -eq $totalOperations) {
        Write-ColorOutput Green "✓ FChassis has been completely uninstalled!"
    }
    else {
        Write-ColorOutput Yellow "⚠ Some operations failed. You may need to run this script again or clean up manually."
    }

    Write-Output ""
    if (-not $KeepThirdParty) {
        Write-ColorOutput Green "All files and registry entries have been removed."
    }
    else {
        Write-ColorOutput Yellow "Third-party components were kept intact. Use -KeepThirdParty:`$false to remove them."
    }
    
    Write-Output ""
    pause
}

# Run the main function
Main