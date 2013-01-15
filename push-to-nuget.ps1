.\.nuget\nuget.exe pack .\Raven.Bundles.TemporalVersioning\Raven.Bundles.TemporalVersioning.csproj
.\.nuget\nuget.exe pack .\Raven.Client.Bundles.TemporalVersioning\Raven.Client.Bundles.TemporalVersioning.csproj
.\.nuget\nuget.exe push *.nupkg
del *.nupkg