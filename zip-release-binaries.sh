#!/bin/bash
set -eu

rid=$1

SOLUTION_NAME="./mysql-data-mover.sln"
dotnet restore ${SOLUTION_NAME}
dotnet build --no-restore --configuration Release ${SOLUTION_NAME}
dotnet test --no-restore --configuration Release ${SOLUTION_NAME}
dotnet publish -r "${rid}" /p:PublishSingleFile=true -c Release --output "./output/${rid}" ${SOLUTION_NAME}

mkdir -p ./mysql-data-mover
cp -r "./output/${rid}/" "./mysql-data-mover/${rid}"

ls -l "./mysql-data-mover/${rid}"
zip -r "./mysql-data-mover/mysql-data-mover_${rid}.zip" "./mysql-data-mover/${rid}"
