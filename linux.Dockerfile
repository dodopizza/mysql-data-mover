FROM dodopizza/mysql-data-mover-platforms:latest as builder

FROM mcr.microsoft.com/dotnet/core/runtime:3.1
WORKDIR /app
COPY --from=builder ./output/linux-x64/ .
CMD [ "/app/Dodo.DataMover" ]
