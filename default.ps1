properties {
	$base_directory = Resolve-Path .
	$src_directory = "$base_directory\source"
	$output_directory = "$base_directory\build"
	$dist_directory = "$base_directory\distribution"
	$sln_file = "$src_directory\IdentityServer3.sln"
	$target_config = "Release"
	$framework_version = "v4.7"
	$xunit_path = "$src_directory\packages\xunit.runner.console.2.4.1\tools\net452\xunit.console.exe"
	$ilmerge_path = "$src_directory\packages\ILMerge.3.0.41\tools\net452\ILMerge.exe"
	$nuget_path = "$base_directory\nuget.exe"

	$buildNumber = 0;
	$version = "2.8.18.$buildNumber"
	$preRelease = $null
}

task default -depends Clean, RunTests, CreateNuGetPackage
task appVeyor -depends Clean, CreateNuGetPackage

task Clean {
	rmdir $output_directory -ea SilentlyContinue -recurse
	rmdir $dist_directory -ea SilentlyContinue -recurse
	exec { . "C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe" /nologo /verbosity:quiet $sln_file /p:Configuration=$target_config /t:Clean }
}

task Compile -depends UpdateVersion {
	exec { . "C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe" /nologo /verbosity:q $sln_file /p:Configuration=$target_config /p:TargetFrameworkVersion=v4.7.2 }

	if ($LastExitCode -ne 0) {
        exit $LastExitCode
    }
}

task UpdateVersion {
	$vSplit = $version.Split('.')
	if($vSplit.Length -ne 4)
	{
		throw "Version number is invalid. Must be in the form of 0.0.0.0"
	}
	$major = $vSplit[0]
	$minor = $vSplit[1]
	$patch = $vSplit[2]
	$assemblyFileVersion =  "$major.$minor.$patch.$buildNumber"
	$assemblyVersion = "$major.$minor.0.0"
	$versionAssemblyInfoFile = "$src_directory/VersionAssemblyInfo.cs"
	"using System.Reflection;" > $versionAssemblyInfoFile
	"" >> $versionAssemblyInfoFile
	"[assembly: AssemblyVersion(""$assemblyVersion"")]" >> $versionAssemblyInfoFile
	"[assembly: AssemblyFileVersion(""$assemblyFileVersion"")]" >> $versionAssemblyInfoFile
}

task RunTests -depends Compile {
	$project = "IdentityServer3.Tests"
	mkdir $output_directory\xunit\$project -ea SilentlyContinue
	.$xunit_path "$src_directory\Tests\UnitTests\bin\Release\$project.dll"
}


task ILMerge -depends Compile {
	$input_dlls = "$output_directory\IdentityServer3.dll"

	Get-ChildItem -Path $output_directory -Filter *.dll |
		foreach-object {
			# Exclude IdentityServer3.dll as that will be the primary assembly
			if ("$_" -ne "IdentityServer3.dll" -and
			    "$_" -ne "Owin.dll") {
				$input_dlls = "$input_dlls $output_directory\$_"
			}
	}

	New-Item $dist_directory\lib\net47 -Type Directory
	Invoke-Expression "$ilmerge_path /targetplatform:v4 /attr:$src_directory\Version\bin\Release\Version.dll /internalize /allowDup /target:library /out:$dist_directory\lib\net47\IdentityServer3.dll $input_dlls /keyfile:IdentityServer3.snk"
	Copy-Item -Path $dist_directory\lib\net47\* -Destination $base_directory\..\eWay\Tools\IdentityServer3
	Copy-Item -Path $output_directory\System.IdentityModel.Tokens.Jwt.dll -Destination $base_directory\..\eWay\Tools\IdentityServer3
}

task CreateNuGetPackage -depends ILMerge {
	$vSplit = $version.Split('.')
	if($vSplit.Length -ne 4)
	{
		throw "Version number is invalid. Must be in the form of 0.0.0.0"
	}
	$major = $vSplit[0]
	$minor = $vSplit[1]
	$patch = $vSplit[2]
	$packageVersion =  "$major.$minor.$patch"
	if($preRelease){
		$packageVersion = "$packageVersion-$preRelease"
	}
	
	if ($buildNumber -ne 0){
		$packageVersion = $packageVersion + "-build" + $buildNumber.ToString().PadLeft(5,'0')
	}


	copy-item $src_directory\IdentityServer3.nuspec $dist_directory
	copy-item $output_directory\IdentityServer3.xml $dist_directory\lib\net47\
	exec { . $nuget_path pack $dist_directory\IdentityServer3.nuspec -BasePath $dist_directory -OutputDirectory $dist_directory -version $packageVersion }
}
