#!/bin/bash

function log() {
  >&2 echo "$*"
}

root=$(pwd)
PROJECT_PREFIX="$1"
PACKAGE_VERSION="$2"

if [[ -z "$PROJECT_PREFIX" ]]
then
  PROJECT_PREFIX=ronimizy.Razor.Sdk
fi

if [[ -z "$PACKAGE_VERSION" ]]
then
  PACKAGE_VERSION=1.0.0
fi

if [[ ! -f RazorSdk.sln ]]
then
  echo "Script must be run from repository root"
  return 1
fi 

# init razor repo

log "[razor repo init] started"

cd razor

./restore.sh
source ./activate.sh
dotnet restore
dotnet clean
deactivate

log "[razor repo init] finished"
cd "$root"

# run modifier

log "[modifier] started"

dotnet run --project src/"$PROJECT_PREFIX".ModifierTool/"$PROJECT_PREFIX".ModifierTool.csproj modify ./razor --project-prefix "$PROJECT_PREFIX" || { log Failed to run modifier tool; exit 1; }

log "[modifier] finished"
cd "$root"

# pack nuget

log "[pack] started"

cd razor
source ./activate.sh

cd src/Compiler/Microsoft.CodeAnalysis.Razor.Compiler/src/
dotnet pack /p:PackageVersion="$PACKAGE_VERSION" -v:d || { log Failed to pack compiler; exit 1; }
deactivate

log "[pack] finished"
cd "$root"

# reset submodule

if [[ -z "$RAZOR_SDK_SKIP_CLEANUP" ]]
then
  log "[reset] started"
  
  cd razor
  git reset --hard
  
  log "[reset] finished"
  cd "$root"
fi
