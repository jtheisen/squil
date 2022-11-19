[xml]$versionXml = Get-Content version.xml

$branchSuffix = if ($branch -eq "release") { "" } else { "-$branch" }

$major = "$($versionXml.Version.Major)"
$minor = "$($versionXml.Version.Minor)"
$patch = "$($versionXml.Version.Patch)"

$baseVersion = "$major.$minor.$patch$branchSuffix"

Write-Output "##vso[task.setvariable variable=squilversionkey]$baseVersion"
