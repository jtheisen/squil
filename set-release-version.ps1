[xml]$versionXml = Get-Content version.xml

$releasename = "$($versionXml.Version.Release)"

if ($env:overridereleasename)
{
    $releasename = "$releasename~$env:overridereleasename"
}

Write-Output "##vso[release.updatereleasename]$releasename"
