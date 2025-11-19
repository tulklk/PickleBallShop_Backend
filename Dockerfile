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
RUN dotnet publish "PickleBall.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 10000

# Copy published app
COPY --from=publish /app/publish .

# Set environment variables
# Render will set PORT automatically, but we default to 10000
ENV ASPNETCORE_URLS=http://+:10000
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "PickleBall.dll"]
