[xml]$versionXml = Get-Content version.xml

Write-Output "##vso[task.setvariable variable=squilversion]$($versionXml.Version.Display)"
Write-Output "##vso[task.setvariable variable=squilversionwithmajor]$($versionXml.Version.Tags.WithMajor)"
Write-Output "##vso[task.setvariable variable=squilversionwithminor]$($versionXml.Version.Tags.WithMinor)"
Write-Output "##vso[task.setvariable variable=squilversionwithpatch]$($versionXml.Version.Tags.WithPatch)"
Write-Output "##vso[task.setvariable variable=squilversionwithrevision]$($versionXml.Version.Tags.WithRevision)"
Write-Output "##vso[task.setvariable variable=squilversionlatest]$($versionXml.Version.Tags.Latest)"
