from mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY *.sln ./
COPY API/*.csproj ./API/
COPY App/*.csproj ./App/
COPY Infrastructure/*.csproj ./Infrastructure/
COPY Domain/*.csproj ./Domain/

RUN dotnet restore API/API.csproj

COPY API/. ./API/
COPY App/. ./App/
COPY Infrastructure/. ./Infrastructure/
COPY Domain/. ./Domain/

WORKDIR /src/API
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./
ENTRYPOINT ["dotnet", "API.dll"]