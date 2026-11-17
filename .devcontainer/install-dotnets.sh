#!/bin/bash

# downloads installer script https://docs.microsoft.com/en-us/dotnet/core/tools/dotnet-install-script
curl -SL --output dotnet-install.sh https://dot.net/v1/dotnet-install.sh


# Install the .NET SDK version required by the project, via global.json
FILE=global.json
if test -f "$FILE"; then
    echo "installing dotnet via $FILE"
    /bin/bash dotnet-install.sh --verbose --jsonfile $FILE
fi
