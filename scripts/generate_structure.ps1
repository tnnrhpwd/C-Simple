$Path = Get-Location
$Depth = 3
$OutputFile = "$Path\directory_structure.txt"
$GitIgnore = "$Path\.gitignore"

# Read .gitignore and process it into a regex pattern
$GitIgnorePatterns = Get-Content $GitIgnore | Where-Object { $_ -and $_ -notmatch '^#' } | ForEach-Object { [regex]::Escape($_.Trim()) }
$IgnoreRegex = [string]::Join('|', $GitIgnorePatterns)

function Get-Tree ($Path, $Depth, $Indent = "") {
    if ($Depth -eq 0) { return }
    $Items = Get-ChildItem -Path $Path
    foreach ($Item in $Items) {
        if ($Item.FullName -notmatch $IgnoreRegex) {
            "$Indent$($Item.Name)" | Out-File -Append -FilePath $OutputFile
            if ($Item.PSIsContainer) {
                Get-Tree -Path $Item.FullName -Depth ($Depth - 1) -Indent ("$Indent  ")
            }
        }
    }
}

Remove-Item $OutputFile -ErrorAction SilentlyContinue
Get-Tree -Path $Path -Depth $Depth
