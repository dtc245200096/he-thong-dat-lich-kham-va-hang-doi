# Build Stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files for caching restore layer
COPY ["src/Shared.Contracts/Shared.Contracts.csproj", "src/Shared.Contracts/"]
COPY ["src/Appointment.Api/Appointment.Api.csproj", "src/Appointment.Api/"]
RUN dotnet restore "src/Appointment.Api/Appointment.Api.csproj"

# Copy source and publish
COPY ["src/Shared.Contracts/", "src/Shared.Contracts/"]
COPY ["src/Appointment.Api/", "src/Appointment.Api/"]
WORKDIR "/src/src/Appointment.Api"
RUN dotnet publish "Appointment.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime Stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Appointment.Api.dll"]

