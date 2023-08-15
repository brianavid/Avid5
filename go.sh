while true
do 
	if [ -f ./Updated.Avid5/Avid5.Net.dll ] 
	then
		echo "Installing new version"
		rm -rf Avid5
		mv Updated.Avid5 Avid5
		mkdir Updated.Avid5
	else
		echo "Re-running unchanged version"
	fi
	
	cd Avid5
	dotnet ./Avid5.Net.dll || break
	cd ..
	
	echo "Restarting ..."; 
done
cd ..
echo "Done"