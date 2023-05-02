$netVersion = "net7.0";

$cmd = $args[0];

if (-Not $cmd)
{
	$cmd = "debug"
}

function PackageCometReload
{
	Write-Host "Building .NET Projects..."

	& dotnet build /r /p:Configuration=Debug ./Comet.Reload/Comet.Reload.csproj

	Write-Host "Done .NET Projects."

	Write-Host "Pack Comet.Reload"

	& dotnet pack NugetBuilds/Clancey.Comet.Reload.nuget.csproj
}

function PackageReloadify3000
{
	Write-Host "Building .NET Projects..."

	& dotnet build /r /p:Configuration=Debug ./Reloadify3000/Reloadify3000.csproj

	& dotnet msbuild /r /p:Configuration=Debug ./Reloadify3000.Build/Reloadify3000.Build.Tasks.csproj

	Write-Host "Done .NET Projects."

	Write-Host "Pack Reloadify3000"

	& dotnet pack NugetBuilds/Reloadify3000.nuget.csproj

}

switch ($cmd) {
	"all" {
		PackageCometReload
		PackageReloadify3000
	}
	"clean" {
		$folders = Get-ChildItem .\ -include bin,obj -Recurse 
		foreach ($_ in $folders) 
		{ 
			remove-item $_.fullname -Force -Recurse 
		}
		Write-Host "Removed all bin and obj folder"
	}
}