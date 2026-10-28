FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/Cartoes.Api/Cartoes.Api.csproj src/Cartoes.Api/
COPY src/Cartoes.Application/Cartoes.Application.csproj src/Cartoes.Application/
COPY src/Cartoes.Domain/Cartoes.Domain.csproj src/Cartoes.Domain/
COPY src/Cartoes.Infrastructure/Cartoes.Infrastructure.csproj src/Cartoes.Infrastructure/
COPY src/Cartoes.Contracts/Cartoes.Contracts.csproj src/Cartoes.Contracts/
RUN dotnet restore src/Cartoes.Api/Cartoes.Api.csproj

COPY src/ src/
RUN dotnet publish src/Cartoes.Api/Cartoes.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID

ENTRYPOINT ["dotnet", "Cartoes.Api.dll"]
