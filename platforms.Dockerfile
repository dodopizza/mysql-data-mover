FROM mcr.microsoft.com/dotnet/core/sdk:3.1 as builder
COPY src ./src
COPY mysql-data-mover.sln .
ENV SOLUTION_NAME "./mysql-data-mover.sln"
RUN dotnet restore ${SOLUTION_NAME}
RUN dotnet build --no-restore --configuration Release ${SOLUTION_NAME}
RUN dotnet test --no-restore --configuration Release ${SOLUTION_NAME}
RUN dotnet publish -r linux-x64 /p:PublishSingleFile=true -c Release --output ./output/linux-x64 ${SOLUTION_NAME} && \
    dotnet publish -r linux-musl-x64 /p:PublishSingleFile=true -c Release --output ./output/linux-musl-x64 ${SOLUTION_NAME} && \
    dotnet publish -r win-x64 /p:PublishSingleFile=true -c Release --output ./output/win-x64 ${SOLUTION_NAME} && \
    dotnet publish -r osx-x64 /p:PublishSingleFile=true -c Release --output ./output/osx-x64 ${SOLUTION_NAME}
# kick the build 2020-08-06 15:32