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

$mauiRevision = if ($branch -eq "release") { "0" } else { "$revision" }

$withMajor = "$major$branchSuffix"
$withMinor = "$major.$minor$branchSuffix"
$withPatch = "$major.$minor.$patch$branchSuffix"
$withRevision = "$major.$minor.$patch.$revision$branchSuffix"
$mauiWindows = "$major.$minor.$patch.$mauiRevision"

$versionXml.Version.Major = $major
$versionXml.Version.Minor = $minor
$versionXml.Version.Patch = $patch
$versionXml.Version.Revision = $revision
$versionXml.Version.Display = $withRevision
$versionXml.Version.Release = $withPatch
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


