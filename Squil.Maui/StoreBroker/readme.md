[Store Broker ist here](https://github.com/microsoft/StoreBroker)


The pdp directory is initialized with the following command:

```ps
.\Extensions\ConvertFrom-ExistingSubmission.ps1 -AppId 9N8RGM5P5XD2 -Release 221021 -PdpFilename pdp.xml -OutPath ..\Squil\Squil.Maui\pdp
```

This must be executed in a clone of the repo as it depends on other stuff in it.
