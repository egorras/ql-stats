# Build Stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj files and restore
COPY ["src/QLStats/QLStats.csproj", "src/QLStats/"]
COPY ["src/QLStats.ServiceDefaults/QLStats.ServiceDefaults.csproj", "src/QLStats.ServiceDefaults/"]
RUN dotnet restore "src/QLStats/QLStats.csproj"

# Copy everything else and build
COPY . .
WORKDIR "/src/src/QLStats"
RUN dotnet publish "QLStats.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime Stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 80
EXPOSE 443
ENV ASPNETCORE_URLS=http://+:80
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "QLStats.dll"]
