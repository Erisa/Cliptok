FROM mcr.microsoft.com/dotnet/sdk:5.0.400 AS build-env
WORKDIR /app

# Ensure git is installed, even though it usually already is
RUN apt-get update; apt-get install git

# Copy csproj and restore as distinct layers
COPY *.csproj ./
RUN dotnet restore

# Copy source code and build
COPY *.cs Helpers Modules *.sln ./
RUN dotnet build -c Release -o out

# Build runtime image
FROM mcr.microsoft.com/dotnet/runtime:5.0.9-alpine3.13
WORKDIR /app
COPY --from=build-env /app/out .
COPY config.json Lists/* ./
ENTRYPOINT ["dotnet", "Cliptok.dll"]
