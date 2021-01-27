FROM mcr.microsoft.com/dotnet/sdk:5.0 AS build-env
WORKDIR /app

# https://github.com/NuGet/Home/issues/10491
# https://devblogs.microsoft.com/nuget/microsoft-author-signing-certificate-update/
COPY .misc/verisign.crt /usr/local/share/ca-certificates
RUN update-ca-certificates

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
ENTRYPOINT ["dotnet", "MicrosoftBot.dll"]
