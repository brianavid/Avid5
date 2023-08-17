#!/bin/bash
while true
do 
	if [ -f Updated.Avid5/Avid5.Net.dll ] 
	then
		echo "Installing new version"
		mv Avid5 Old.Avid5
		mv Updated.Avid5 Avid5
		mv Old.Avid5/Logs Avid5/Logs
		rm -rf Old.Avid5
		mkdir Updated.Avid5
	else
		echo "Re-running unchanged version"
	fi
	
	cd Avid5
	dotnet Avid5.Net.dll ../Avid5.Config || break
	cd ..
	
	echo "Restarting ..."; 
done

echo "Done"