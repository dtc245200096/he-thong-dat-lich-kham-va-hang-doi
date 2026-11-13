# Build Stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files for caching restore layer
COPY ["src/Shared.Contracts/Shared.Contracts.csproj", "src/Shared.Contracts/"]
COPY ["src/Queue.Worker/Queue.Worker.csproj", "src/Queue.Worker/"]
RUN dotnet restore "src/Queue.Worker/Queue.Worker.csproj"

# Copy source and publish
COPY ["src/Shared.Contracts/", "src/Shared.Contracts/"]
COPY ["src/Queue.Worker/", "src/Queue.Worker/"]
WORKDIR "/src/src/Queue.Worker"
RUN dotnet publish "Queue.Worker.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime Stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Queue.Worker.dll"]

