#!/usr/bin/env bash

set -eu
set -o pipefail

CONFIGURATION=${1:-Debug}

echo "Restoring NuGet packages..."
dotnet restore

echo "Building the solution..."
dotnet build --configuration $CONFIGURATION --no-restore

echo "Running tests..."
dotnet test --configuration $CONFIGURATION --no-build

echo "Build and test completed."
