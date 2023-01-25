FROM mcr.microsoft.com/dotnet/sdk:7.0.102-bullseye-slim as builder
COPY src ./src
COPY mysql-data-mover.sln .
ENV SOLUTION_NAME "./mysql-data-mover.sln"
RUN dotnet restore ${SOLUTION_NAME} && \
    dotnet build --no-restore --configuration Release ${SOLUTION_NAME} && \
    dotnet test --no-restore --configuration Release ${SOLUTION_NAME}
