while ($true)
{
    if ( Test-Path Updated.Avid5\Avid5.Net.dll ) {
		echo  "Installing new version"
		Rename-Item -Path Avid5.Net -NewName Old.Avid5.Net
		Rename-Item -Path Updated.Avid5 -NewName Avid5.Net
		Rename-Item -Path Old.Avid5\Logs -NewName Avid5.Net\Logs
		New-Item -Path Updated.Avid5 -ItemType Directory
		Remove-Item Old.Avid5.Net -Recurse -Force
	} else {
		echo  "Re-running unchanged version"
	}

	cd Avid5.Net
	dotnet Avid5.Net.dll
	cd ..
	if ( $LastExitCode -ne '0' ) { break }

	echo "Restarting ..."; 
}

echo "Done"