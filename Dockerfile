FROM mcr.microsoft.com/dotnet/sdk:5.0 AS build-env
WORKDIR /app

# Copy csproj and restore as distinct layers
COPY *.csproj ./
RUN dotnet restore

# Copy everything else and build
COPY . ./
RUN dotnet build -c Release -o out

# Build runtime image
FROM mcr.microsoft.com/dotnet/runtime:5.0-alpine
WORKDIR /app
COPY --from=build-env /app/out .
ENTRYPOINT ["dotnet", "Cliptok.dll"]
