[xml]$versionXml = Get-Content version.xml

$releasename = "$($versionXml.Version.Display)"

if ($env:overridereleasename)
{
    $releasename = $env:overridereleasename
}

Write-Output "##vso[release.updatereleasename]$releasename"
