$fullversion=$env:VERSION

if ($fullversion)
{
	Write-Output "New version is: $fullversion"

	$mauiWindowsManifestFile = 'Squil.Maui\Platforms\Windows\Package.appxmanifest'
	[xml]$mauiWindowsManifestXml = Get-Content $mauiWindowsManifestFile
	$mauiWindowsManifestXml.Package.Identity.Version = $fullversion
	$mauiWindowsManifestXml.OuterXml | Out-File $mauiWindowsManifestFile
}
else
{
	Write-Warning 'No VERSION found in the environment'

	$fullversion='dev'
}

$fullversion | Out-File version.txt
