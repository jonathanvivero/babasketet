FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Babasketet.API.csproj .
RUN dotnet restore Babasketet.API.csproj

COPY . .
RUN dotnet publish Babasketet.API.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime
WORKDIR /app
RUN apk add --no-cache wget
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "Babasketet.API.dll"]
