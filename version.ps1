$fullversion=$env:SQUILVERSION
$mauiversion=$env:SQUILMAUIVERSION
$dockertags=$env:SQUILDOCKERTAGS

if ($fullversion)
{
	Write-Output "New version is: $fullversion, MAUI version remains $mauiversion, docker tags are:"
	Write-Output "$dockertags"

	$mauiWindowsManifestFile = 'Squil.Maui\Platforms\Windows\Package.appxmanifest'
	[xml]$mauiWindowsManifestXml = Get-Content $mauiWindowsManifestFile
	$mauiWindowsManifestXml.Package.Identity.Version = $mauiversion
	$mauiWindowsManifestXml.OuterXml | Out-File $mauiWindowsManifestFile
}
else
{
	Write-Warning 'No VERSION found in the environment'

	$fullversion='dev'
}

$fullversion | Out-File version.txt
$dockertags | Out-File dockertags.txt

