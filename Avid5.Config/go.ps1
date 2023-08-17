
while ($true)
{
    if ( Test-Path Updated.Avid5\Avid5.Net.dll ) 
	{
		echo  "Installing new version"
		Remove-Item Avid5.Net -Recurse -Force
		Rename-Item -Path Updated.Avid5 -NewName Avid5.Net
		New-Item -Path Updated.Avid5 -ItemType Directory
	} else {
		echo  "Re-running unchanged version"
	}

	cd Avid5.Net
	dotnet Avid5.Net.dll ..\Avid5.Config
	cd ..
	if ( $LastExitCode -ne '0' ) { break }

	echo "Restarting ..."; 
}

echo "Done"