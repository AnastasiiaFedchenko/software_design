FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS runtime
WORKDIR /app
COPY --from=build /app/FlowerShop/CLI/bin/Release/net7.0/publish/ .
ENTRYPOINT ["dotnet", "CLI.dll"]