FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0-jammy as builder

# Must be linux/amd64 or linux/arm64
ARG TARGETARCH

COPY src ./src
COPY mysql-data-mover.sln .

ENV SOLUTION_NAME "./mysql-data-mover.sln"

RUN dotnet restore ${SOLUTION_NAME}
RUN dotnet build --no-restore --configuration Release ${SOLUTION_NAME}
RUN dotnet test --no-restore --configuration Release ${SOLUTION_NAME}
RUN dotnet publish --no-restore --configuration Release --property:PublishDir=/output/${TARGETARCH} ${SOLUTION_NAME}

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/runtime:8.0-jammy-chiseled

# Must be linux/amd64 or linux/arm64
ARG TARGETARCH

WORKDIR /app
COPY --from=builder ./output/${TARGETARCH} .
ENTRYPOINT ["dotnet", "Dodo.DataMover.dll"]
