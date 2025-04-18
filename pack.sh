#!/bin/bash

root=$(pwd)

# init razor repo

cd razor

./restore.sh
source ./activate.sh
dotnet restore
dotnet clean
deactivate

cd "$root"

# run modifier

cd src/RazorSdk.ModifierTool

dotnet run modify ./razor --project-prefix ronimizy.Razor.Sdk
cd "$root"

# pack nuget

cd razor
source ./activate.sh

cd src/Compiler/Microsoft.CodeAnalysis.Razor.Compiler/src/
dotnet pack /p:PackageVersion=11.0.1 /p:TargetFramework=netstandard2.0 /p:TargetFrameworks=netstandard2.0;
deactivate

cd "$root"

# reset submodule

cd razor
git reset --hard

cd "$root"


