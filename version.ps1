$fullversion=$env:SQUILVERSION
$mauiversion=$env:SQUILMAUIVERSION
$dockertags=$env:SQUILDOCKERTAGS

if ($fullversion)
{
	Write-Output "New version is: $fullversion, MAUI version remains $mauiversion"
	Write-Information "Docker tags are:\n$dockertags"

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
