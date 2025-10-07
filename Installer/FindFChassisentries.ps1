<# 
.SYNOPSIS
  Finds:
   1) Registry entries under HKLM:\SOFTWARE where the value name OR value data 
      is exactly 'FChassis'
   2) Folders named exactly 'FChassis'
#>

$target = "FChassis"

Write-Host "=== Searching Registry (HKLM:\SOFTWARE) for exact '$target' ==="

# Search registry
Get-ChildItem -Path "HKLM:\SOFTWARE" -Recurse -ErrorAction SilentlyContinue | ForEach-Object {
    try {
        $props = Get-ItemProperty -Path $_.PsPath -ErrorAction SilentlyContinue
        if ($props) {
            foreach ($prop in $props.PSObject.Properties) {
                $name  = $prop.Name
                $value = $prop.Value

                # Exact match on value name
                if ($name -ceq $target) {
                    Write-Host "Registry Match: Value Name = '$name' at $($_.PsPath)"
                }

                # Exact match on value data (force to string for comparison)
                if ($null -ne $value -and ($value.ToString() -ceq $target)) {
                    Write-Host "Registry Match: Value Data = '$value' (Name '$name') at $($_.PsPath)"
                }
            }
        }
    }
    catch {
        # Skip keys we can’t access
    }
}

Write-Host "`n=== Searching File System for folders named exactly '$target' ==="

# Search all drives for folder named exactly 1.0.4
Get-PSDrive -PSProvider FileSystem | ForEach-Object {
    Get-ChildItem -Path $_.Root -Directory -Recurse -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -ceq $target } |
        ForEach-Object {
            Write-Host "Folder Match: $($_.FullName)"
        }
}
