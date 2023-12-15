FROM dodopizza/mysql-data-mover-platforms as builder

FROM mcr.microsoft.com/dotnet/runtime:8.0-jammy-chiseled
WORKDIR /app
COPY --from=builder ./output/linux-x64/ .
CMD [ "/app/Dodo.DataMover" ]
