# FChassis Cleanup Script
# Requires administrative privileges
# This script removes all registry entries, environment variables, and system changes made by FChassis installer

# Add assembly for Windows Forms (for popup messages)
Add-Type -AssemblyName System.Windows.Forms

# Function to display popup message
function Show-Popup {
    param(
        [string]$Message,
        [string]$Title,
        [string]$Icon = "Info"
    )
    
    $iconType = switch ($Icon) {
        "Info" { [System.Windows.Forms.MessageBoxIcon]::Information }
        "Warning" { [System.Windows.Forms.MessageBoxIcon]::Warning }
        "Error" { [System.Windows.Forms.MessageBoxIcon]::Error }
        "Question" { [System.Windows.Forms.MessageBoxIcon]::Question }
        default { [System.Windows.Forms.MessageBoxIcon]::Information }
    }
    
    [System.Windows.Forms.MessageBox]::Show($Message, $Title, [System.Windows.Forms.MessageBoxButtons]::OK, $iconType)
}

# Function to comprehensively scan and remove specific FChassis path entries from registry
function Remove-FChassisPathRegistryEntries {
    Write-Host "Starting comprehensive registry scan for FChassis path entries..." -ForegroundColor Cyan
    
    # Define specific path patterns to search for (as requested)
    $patterns = @(
        "*C:|FChassis*",
        "*C:\FChassis*"
    )
    
    # Registry hives to scan
    $registryHives = @(
        "HKLM:",
        "HKCU:",
        "HKCR:"
    )
    
    $removedCount = 0
    
    foreach ($hive in $registryHives) {
        Write-Host "Scanning registry hive: $hive" -ForegroundColor Yellow
        
        try {
            # Get top-level keys in the hive
            $topLevelKeys = Get-ChildItem -Path $hive -ErrorAction SilentlyContinue
            
            foreach ($key in $topLevelKeys) {
                Write-Host "Scanning key: $($key.Name)" -ForegroundColor Gray
                
                foreach ($pattern in $patterns) {
                    try {
                        # Search for keys matching the pattern
                        $matchingKeys = Get-ChildItem -Path $key.PSPath -Recurse -ErrorAction SilentlyContinue | 
                                        Where-Object { $_.PSChildName -like $pattern }
                        
                        foreach ($matchingKey in $matchingKeys) {
                            Write-Host "Removing matching key: $($matchingKey.PSPath)" -ForegroundColor Yellow
                            Remove-Item -Path $matchingKey.PSPath -Recurse -Force -ErrorAction SilentlyContinue
                            Write-Host "Removed key: $($matchingKey.PSPath)" -ForegroundColor Green
                            $removedCount++
                        }
                        
                        # Search for values matching the pattern in all subkeys
                        $allKeys = Get-ChildItem -Path $key.PSPath -Recurse -ErrorAction SilentlyContinue
                        foreach ($subKey in $allKeys) {
                            try {
                                $properties = Get-ItemProperty -Path $subKey.PSPath -ErrorAction SilentlyContinue
                                $properties.PSObject.Properties | Where-Object {
                                    $_.Name -notlike "PS*" -and 
                                    ($_.Name -like $pattern -or $_.Value -like $pattern)
                                } | ForEach-Object {
                                    Write-Host "Removing value from $($subKey.PSPath): $($_.Name) = $($_.Value)" -ForegroundColor Yellow
                                    Remove-ItemProperty -Path $subKey.PSPath -Name $_.Name -Force -ErrorAction SilentlyContinue
                                    Write-Host "Removed value: $($_.Name)" -ForegroundColor Green
                                    $removedCount++
                                }
                            } catch {
                                # Continue if we can't access the key
                                continue
                            }
                        }
                    } catch {
                        Write-Host "Error scanning $($key.PSPath) for pattern '$pattern': $($_.Exception.Message)" -ForegroundColor Red
                    }
                }
            }
        } catch {
            Write-Host "Error accessing hive $hive : $($_.Exception.Message)" -ForegroundColor Red
        }
    }
    
    Write-Host "Comprehensive registry scan completed. Removed $removedCount entries." -ForegroundColor Green
}

# Check for administrator privileges
if (-NOT ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")) {
    # Create a popup to request elevation
    $result = [System.Windows.Forms.MessageBox]::Show(
        "This FChassis cleanup script requires administrator privileges.`n`nDo you want to restart with elevated permissions?", 
        "Administrator Rights Required", 
        [System.Windows.Forms.MessageBoxButtons]::YesNo, 
        [System.Windows.Forms.MessageBoxIcon]::Question
    )
    
    if ($result -eq [System.Windows.Forms.DialogResult]::Yes) {
        # Restart script with elevated privileges
        $psi = New-Object System.Diagnostics.ProcessStartInfo
        $psi.FileName = "powershell.exe"
        $psi.Arguments = "-ExecutionPolicy Bypass -File `"$($MyInvocation.MyCommand.Path)`""
        $psi.Verb = "runas"  # This triggers UAC elevation
        
        try {
            [System.Diagnostics.Process]::Start($psi) | Out-Null
            exit 0
        } catch {
            Show-Popup "Failed to elevate permissions. Please run this script as Administrator." "Error" "Error"
            exit 1
        }
    } else {
        Show-Popup "Cleanup cancelled. Script requires administrator privileges to continue." "Cancelled" "Warning"
        exit 1
    }
}

# Main cleanup process
try {
    $appName = "FChassis"
    $company = "Teckinsoft Neuronics Pvt. Ltd."
    $installDir = "C:\FChassis"  # Default installation directory

    Write-Host "Starting FChassis cleanup..." -ForegroundColor Yellow
    Show-Popup "Starting FChassis cleanup process. This will remove all C:\\FChassis and C:|FChassis registry entries." "FChassis Cleanup" "Info"

    # 0. Comprehensive registry scan for specific path patterns first
    Remove-FChassisPathRegistryEntries

    # 1. Remove specific known registry entries
    Write-Host "Removing specific known registry entries..." -ForegroundColor Cyan

    $uninstallKey = "HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\$appName"
    $installFlagKey = "HKLM:\Software\$company\$appName"

    if (Test-Path $uninstallKey) {
        Remove-Item -Path $uninstallKey -Recurse -Force
        Write-Host "Removed uninstall registry key" -ForegroundColor Green
    }

    if (Test-Path $installFlagKey) {
        Remove-Item -Path $installFlagKey -Recurse -Force
        Write-Host "Removed install flag registry key" -ForegroundColor Green
    }

    # 2. Remove zombie registry entries in HKLM:\SOFTWARE\Classes\Installer\Assemblies
    Write-Host "Removing Installer Assemblies entries..." -ForegroundColor Cyan
    $assembliesPath = "HKLM:\SOFTWARE\Classes\Installer\Assemblies"
    
    if (Test-Path $assembliesPath) {
        # Look for entries with the specific path patterns
        $zombiePatterns = @(
            "*C:|FChassis*",
            "*C:\FChassis*",
            "*System.DirectoryServices.AccountManagement.dll*",
            "*System.DirectoryServices.Protocols.dll*",
            "*System.Management.dll*"
        )
        
        foreach ($pattern in $zombiePatterns) {
            try {
                $matchingKeys = Get-ChildItem -Path $assembliesPath -Recurse -ErrorAction SilentlyContinue | 
                                Where-Object { $_.PSChildName -like $pattern }
                
                foreach ($matchingKey in $matchingKeys) {
                    Write-Host "Removing: $($matchingKey.PSPath)" -ForegroundColor Yellow
                    Remove-Item -Path $matchingKey.PSPath -Recurse -Force -ErrorAction SilentlyContinue
                    Write-Host "Removed: $($matchingKey.PSPath)" -ForegroundColor Green
                }
            } catch {
                Write-Host "Error processing pattern '$pattern': $($_.Exception.Message)" -ForegroundColor Red
            }
        }
    }

    # 3. Remove Installer Folders entries
    Write-Host "Removing Installer Folders entries..." -ForegroundColor Cyan
    $installerFoldersPath = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Installer\Folders"
    if (Test-Path $installerFoldersPath) {
        try {
            # Get all property names (which are the folder paths)
            $properties = (Get-Item -Path $installerFoldersPath).Property
            
            foreach ($property in $properties) {
                # Check if the property name (folder path) contains our patterns
                if ($property -like "*C:|FChassis*" -or $property -like "*C:\FChassis*") {
                    Write-Host "Removing folder reference: $property" -ForegroundColor Yellow
                    Remove-ItemProperty -Path $installerFoldersPath -Name $property -Force -ErrorAction SilentlyContinue
                    Write-Host "Removed folder reference: $property" -ForegroundColor Green
                }
            }
        } catch {
            Write-Host "Error processing Installer Folders: $($_.Exception.Message)" -ForegroundColor Red
        }
    }

    # 4. Remove RADAR HeapLeakDetection entry
    Write-Host "Removing RADAR HeapLeakDetection entry..." -ForegroundColor Cyan
    $radarPath = "HKLM:\SOFTWARE\Microsoft\RADAR\HeapLeakDetection\DiagnosedApplications\FChassis.exe"
    if (Test-Path $radarPath) {
        Remove-Item -Path $radarPath -Recurse -Force -ErrorAction SilentlyContinue
        Write-Host "Removed RADAR HeapLeakDetection entry" -ForegroundColor Green
    }

    # 5. Remove environment variables from PATH
    Write-Host "Removing environment variables from PATH..." -ForegroundColor Cyan

    $pathDirsToRemove = @(
        "$installDir\thirdParty\OpenCASCADE-7.7.0-vc14-64\draco-1.4.1-vc14-64\bin",
        "$installDir\thirdParty\OpenCASCADE-7.7.0-vc14-64\opencascade-7.7.0\win64\vc14\bin",
        "$installDir\thirdParty\OpenCASCADE-7.7.0-vc14-64\ffmpeg-3.3.4-64\bin",
        "$installDir\thirdParty\OpenCASCADE-7.7.0-vc14-64\freeimage-3.17.0-vc14-64\bin",
        "$installDir\thirdParty\OpenCASCADE-7.7.0-vc14-64\freetype-2.5.5-vc14-64\bin",
        "$installDir\thirdParty\OpenCASCADE-7.7.0-vc14-64\openvr-1.14.15-64\bin\win64",
        "$installDir\thirdParty\OpenCASCADE-7.7.0-vc14-64\qt5.11.2-vc14-64\bin",
        "$installDir\thirdParty\OpenCASCADE-7.7.0-vc14-64\rapidjson-1.1.0\bin",
        "$installDir\thirdParty\OpenCASCADE-7.7.0-vc14-64\tbb_2021.5-vc14-64\bin",
        "$installDir\thirdParty\OpenCASCADE-7.7.0-vc14-64\tcltk-86-64\bin",
        "$installDir\thirdParty\OpenCASCADE-7.7.0-vc14-64\vtk-6.1.0-vc14-64\bin"
    )

    # Remove from system PATH
    $systemPath = [Environment]::GetEnvironmentVariable("Path", "Machine")
    $originalPath = $systemPath
    foreach ($dir in $pathDirsToRemove) {
        $systemPath = $systemPath -replace [regex]::Escape($dir) + ';?', ''
    }
    
    if ($systemPath -ne $originalPath) {
        [Environment]::SetEnvironmentVariable("Path", $systemPath.Trim(';'), "Machine")
        Write-Host "Removed FChassis directories from system PATH" -ForegroundColor Green
    }

    # 6. Remove CASROOT environment variable
    Write-Host "Removing CASROOT environment variable..." -ForegroundColor Cyan
    [Environment]::SetEnvironmentVariable("CASROOT", $null, "Machine")
    Write-Host "Removed CASROOT environment variable" -ForegroundColor Green

    # 7. Remove virtual drive mapping
    Write-Host "Removing virtual drive mapping..." -ForegroundColor Cyan
    try {
        & subst W: /D 2>$null
        Write-Host "Removed W: drive mapping" -ForegroundColor Green
    } catch {
        Write-Host "No W: drive mapping found or could not remove" -ForegroundColor Yellow
    }

    # 8. Remove startup entry for virtual drive
    Write-Host "Removing startup entry..." -ForegroundColor Cyan
    $startupKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"
    if (Test-Path $startupKey) {
        Remove-ItemProperty -Path $startupKey -Name "MyVirtualDrive" -ErrorAction SilentlyContinue
        Write-Host "Removed startup entry" -ForegroundColor Green
    }

    # 9. Remove shortcuts
    Write-Host "Removing shortcuts..." -ForegroundColor Cyan

    # All Users Start Menu shortcut
    $startMenuPath = "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\$appName"
    if (Test-Path "$startMenuPath\$appName.lnk") {
        Remove-Item "$startMenuPath\$appName.lnk" -Force
        Write-Host "Removed Start Menu shortcut" -ForegroundColor Green
    }
    if (Test-Path $startMenuPath) {
        Remove-Item $startMenuPath -Recurse -Force -ErrorAction SilentlyContinue
        Write-Host "Removed Start Menu folder" -ForegroundColor Green
    }

    # Desktop shortcut (all users)
    $desktopPath = [Environment]::GetFolderPath("CommonDesktopDirectory")
    if (Test-Path "$desktopPath\$appName.lnk") {
        Remove-Item "$desktopPath\$appName.lnk" -Force
        Write-Host "Removed Desktop shortcut" -ForegroundColor Green
    }

    # 10. Remove installation directory
    Write-Host "Removing installation directory..." -ForegroundColor Cyan
    if (Test-Path $installDir) {
        try {
            Remove-Item -Path $installDir -Recurse -Force -ErrorAction Stop
            Write-Host "Removed installation directory: $installDir" -ForegroundColor Green
        } catch {
            Write-Host "Could not fully remove installation directory. Some files may be in use." -ForegroundColor Red
            Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
        }
    } else {
        Write-Host "Installation directory not found: $installDir" -ForegroundColor Yellow
    }

    # 11. Broadcast environment changes to make them take effect immediately
    Write-Host "Broadcasting environment changes..." -ForegroundColor Cyan
    try {
        # Notify system of environment changes
        $signature = @'
[DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
public static extern IntPtr SendMessageTimeout(
    IntPtr hWnd,
    uint Msg,
    UIntPtr wParam,
    string lParam,
    uint fuFlags,
    uint uTimeout,
    out UIntPtr lpdwResult);
'@
        $type = Add-Type -Name "Win32SendMessageTimeout" -Namespace Win32Functions -MemberDefinition $signature -UsingNamespace System.Text -PassThru
        $HWND_BROADCAST = [IntPtr]0xffff
        $WM_SETTINGCHANGE = 0x1A
        $result = $type::SendMessageTimeout($HWND_BROADCAST, $WM_SETTINGCHANGE, [UIntPtr]::Zero, "Environment", 2, 5000, [ref]([UIntPtr]::Zero))
        Write-Host "Environment changes broadcasted" -ForegroundColor Green
    } catch {
        Write-Host "Could not broadcast environment changes (may require restart)" -ForegroundColor Yellow
    }

    # Success message
    $successMsg = "FChassis cleanup completed successfully!`n`nThe following items were removed:`n- All registry entries containing C:\\FChassis and C:|FChassis paths`n- Specific registry keys and values`n- RADAR HeapLeakDetection entry`n- Installer Folders entries`n- Environment variables`n- Start Menu and Desktop shortcuts`n- Virtual drive mapping`n- Installation directory`n`nYou may need to restart your computer for all changes to take full effect."
    Write-Host "`n$successMsg" -ForegroundColor Green
    Show-Popup $successMsg "FChassis Cleanup Complete" "Info"

} catch {
    $errorMsg = "An error occurred during cleanup: $($_.Exception.Message)`n`nSome items may not have been completely removed."
    Write-Host $errorMsg -ForegroundColor Red
    Show-Popup $errorMsg "Cleanup Error" "Error"
}