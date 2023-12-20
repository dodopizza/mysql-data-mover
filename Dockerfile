FROM mcr.microsoft.com/dotnet/sdk:8.0-jammy as builder
COPY src ./src
COPY mysql-data-mover.sln .

ENV SOLUTION_NAME "./mysql-data-mover.sln"

RUN dotnet restore ${SOLUTION_NAME}
RUN dotnet build --no-restore --configuration Release ${SOLUTION_NAME}
RUN dotnet test --no-restore --configuration Release ${SOLUTION_NAME}
RUN dotnet publish --no-restore --configuration Release -o output ${SOLUTION_NAME}

FROM mcr.microsoft.com/dotnet/runtime:8.0-jammy-chiseled
WORKDIR /app
COPY --from=builder ./output/ .
ENTRYPOINT ["dotnet", "Dodo.DataMover.dll"]
