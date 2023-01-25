FROM dodopizza/mysql-data-mover-platforms as builder

FROM mcr.microsoft.com/dotnet/runtime:7.0.2-bullseye-slim
WORKDIR /app
COPY --from=builder ./output/linux-x64/ .
CMD [ "/app/Dodo.DataMover" ]
