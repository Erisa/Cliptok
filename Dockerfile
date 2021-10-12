FROM mcr.microsoft.com/dotnet/sdk:5.0.402 AS build-env
WORKDIR /app

# Copy csproj and restore as distinct layers
COPY *.csproj ./
RUN dotnet restore

# Copy source code and build
COPY *.cs Helpers Modules *.sln ./
RUN dotnet build -c Release -o out

# We already have this image pulled, its actually quicker to reuse it
FROM mcr.microsoft.com/dotnet/sdk:5.0.402 AS git-collector
WORKDIR /out
COPY . .
RUN touch dummy.txt && \
    if [ -d .git ]; then \
        git rev-parse --short HEAD > CommitHash.txt && \
        git log --pretty=format:"%s" -n 1 > CommitMessage.txt && \
        git log --pretty=format:"%ci" -n 1 > CommitTime.txt; \
    fi

# Build runtime image
FROM mcr.microsoft.com/dotnet/runtime:5.0.11-alpine3.13
LABEL com.centurylinklabs.watchtower.enable true
WORKDIR /app
COPY --from=build-env /app/out .
ADD Lists ./Lists
ADD config.json ./
COPY --from=git-collector /out/*.txt ./
ENTRYPOINT ["dotnet", "Cliptok.dll"]