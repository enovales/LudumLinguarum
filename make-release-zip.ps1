If (Test-Path 'bin\LudumLinguarum.zip') {
	Remove-Item 'bin\LudumLinguarum.zip'
}

Remove-Item 'bin\LudumLinguarumConsole\*.pdb'
Remove-Item 'bin\LudumLinguarumConsole\*.xml'

Add-Type -A System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::CreateFromDirectory('bin\LudumLinguarumConsole', 'bin\LudumLinguarum.zip')

