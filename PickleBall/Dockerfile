# Use the official .NET 8 SDK image for building
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj files and restore dependencies
COPY ["PickleBall/PickleBall.csproj", "PickleBall/"]
COPY ["PickleBall/PickleBall.Domain/PickleBall.Domain.csproj", "PickleBall/PickleBall.Domain/"]
COPY ["PickleBall/PickleBall.Application/PickleBall.Application.csproj", "PickleBall/PickleBall.Application/"]
COPY ["PickleBall/PickleBall.Infrastructure/PickleBall.Infrastructure.csproj", "PickleBall/PickleBall.Infrastructure/"]

RUN dotnet restore "PickleBall/PickleBall.csproj"

# Copy everything else and build
COPY PickleBall/ PickleBall/
WORKDIR "/src/PickleBall"
RUN dotnet build "PickleBall.csproj" -c Release -o /app/build

# Publish the application
FROM build AS publish
WORKDIR "/src/PickleBall"
RUN dotnet publish "PickleBall.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 8080

# Copy published app
COPY --from=publish /app/publish .

# Set environment variables
# Fly.io will set PORT automatically, but we default to 8080
ENV PORT=8080
ENV ASPNETCORE_URLS=http://+:$PORT
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "PickleBall.dll"]
