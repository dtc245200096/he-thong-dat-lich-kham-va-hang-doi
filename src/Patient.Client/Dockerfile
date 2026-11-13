# Build Stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["src/Shared.Contracts/Shared.Contracts.csproj", "src/Shared.Contracts/"]
COPY ["src/Patient.Client/Patient.Client.csproj", "src/Patient.Client/"]
RUN dotnet restore "src/Patient.Client/Patient.Client.csproj"

COPY ["src/Shared.Contracts/", "src/Shared.Contracts/"]
COPY ["src/Patient.Client/", "src/Patient.Client/"]
WORKDIR "/src/src/Patient.Client"
RUN dotnet publish "Patient.Client.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime Stage
FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Patient.Client.dll"]

