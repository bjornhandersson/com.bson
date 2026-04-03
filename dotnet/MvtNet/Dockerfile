FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /src

COPY MvtNet/MvtNet.csproj MvtNet/
COPY MvtNet.Demo.Api/MvtNet.Demo.Api.csproj MvtNet.Demo.Api/

ENV SKIP_PROTOC=true
RUN dotnet restore MvtNet.Demo.Api/MvtNet.Demo.Api.csproj

COPY MvtNet/ MvtNet/
COPY MvtNet.Demo.Api/ MvtNet.Demo.Api/

# Uses pre-generated VectorTile.cs in Generated/ — skips protoc via SKIP_PROTOC
RUN dotnet publish MvtNet.Demo.Api/MvtNet.Demo.Api.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "MvtNet.Demo.Api.dll"]
