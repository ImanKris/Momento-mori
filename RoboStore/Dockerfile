FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["RoboStore.csproj", "./"]
RUN dotnet restore "RoboStore.csproj"
COPY . .
RUN dotnet build "RoboStore.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "RoboStore.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "RoboStore.dll"]