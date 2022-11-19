$major = "$env:SQUILVERSIONMAJOR"
$minor = "$env:SQUILVERSIONMINOR"
$patch = "$env:SQUILVERSIONPATCH"
$revision = "$env:SQUILREVISION"
$branch = "$env:BRANCHNAME"

function Format-Xml ([xml]$xml, $indent=2)
{
    $StringWriter = New-Object System.IO.StringWriter
    $XmlWriter = New-Object System.XMl.XmlTextWriter $StringWriter
    $xmlWriter.Formatting = "indented"
    $xmlWriter.Indentation = $indent
    $xml.WriteContentTo($XmlWriter)
    $XmlWriter.Flush()
    $StringWriter.Flush()
    return $StringWriter.ToString()
}

[xml]$versionXml = Get-Content version.xml

$branchSuffix = if ($branch -eq "release") { "" } else { "-$branch" }

$withMajor = "$major$branchSuffix"
$withMinor = "$major.$minor$branchSuffix"
$withPatch = "$major.$minor.$patch$branchSuffix"
$withRevision = "$major.$minor.$patch.$revision$branchSuffix"
$mauiWindows = "$major.$minor.$patch.0"

$versionXml.Version.Revision = $revision
$versionXml.Version.Display = $withRevision
$versionXml.Version.MauiWindows = $mauiWindows
$versionXml.Version.Tags.WithMajor = "$withMajor"
$versionXml.Version.Tags.WithMinor = "$withMinor"
$versionXml.Version.Tags.WithPatch = "$withPatch"
$versionXml.Version.Tags.WithRevision = "$withRevision"
$versionXml.Version.Tags.Latest = "latest$branchSuffix"

$formattedXml = Format-Xml($versionXml)

$formattedXml | Write-Output

if (-not $branch)
{
    Write-Warning "Branch name not set, won't continue"

    Break Script
}

if (-not $revision)
{
    Write-Warning "No revision set, won't continue'"

    Break Script
}

# Update version.xml
$formattedXml | Out-File version.xml

# Update MAUI Version
$mauiWindowsManifestFile = 'Squil.Maui\Platforms\Windows\Package.appxmanifest'
[xml]$mauiWindowsManifestXml = Get-Content $mauiWindowsManifestFile
$mauiWindowsManifestXml.Package.Identity.Version = $mauiWindows
Format-Xml($mauiWindowsManifestXml) | Out-File $mauiWindowsManifestFile


