
while ($true)
{
    if ( Test-Path Updated.Avid5\Avid5.Net.dll ) 
	{
		echo  "Installing new version"
		Rename-Item -Path Avid5 -NewName Old.Avid5
		Rename-Item -Path Updated.Avid5 -NewName Avid5
		move-Item -Path Old.Avid5\Logs -Destination Avid5\Logs
		Remove-Item Old.Avid5 -Recurse -Force
		New-Item -Path Updated.Avid5 -ItemType Directory
	} else {
		echo  "Re-running unchanged version"
	}

	cd Avid5
	dotnet Avid5.Net.dll ..\Avid5.Config
	cd ..
	if ( $LastExitCode -ne '0' ) { break }

	echo "Restarting ..."; 
}

echo "Done"