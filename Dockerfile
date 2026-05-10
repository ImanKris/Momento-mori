FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["RoboStore/RoboStore.csproj", "RoboStore/"]
RUN dotnet restore "RoboStore/RoboStore.csproj"
COPY RoboStore/ RoboStore/
RUN dotnet build "RoboStore/RoboStore.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "RoboStore/RoboStore.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "RoboStore.dll"]
