# Build Stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files for caching restore layer
COPY ["src/Shared.Contracts/Shared.Contracts.csproj", "src/Shared.Contracts/"]
COPY ["src/Notification.Service/Notification.Service.csproj", "src/Notification.Service/"]
RUN dotnet restore "src/Notification.Service/Notification.Service.csproj"

# Copy source and publish
COPY ["src/Shared.Contracts/", "src/Shared.Contracts/"]
COPY ["src/Notification.Service/", "src/Notification.Service/"]
WORKDIR "/src/src/Notification.Service"
RUN dotnet publish "Notification.Service.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime Stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Notification.Service.dll"]

