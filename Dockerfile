FROM --platform=${BUILDPLATFORM} \
    mcr.microsoft.com/dotnet/sdk:9.0.301 AS build-env
WORKDIR /app

# Copy csproj and restore as distinct layers
COPY *.csproj ./
RUN dotnet restore

# Copy source code and build
COPY . ./
RUN dotnet publish Cliptok.csproj -c Release --property:PublishDir=$PWD/out

# We already have this image pulled, its actually quicker to reuse it
FROM mcr.microsoft.com/dotnet/sdk:9.0.301 AS git-collector
WORKDIR /out
COPY . .
RUN touch dummy.txt && \
    if [ -d .git ]; then \
        git rev-parse --short HEAD > CommitHash.txt && \
        git log --pretty=format:"%s" -n 1 > CommitMessage.txt && \
        git log --pretty=format:"%ci" -n 1 > CommitTime.txt; \
    fi

# Build runtime image
FROM mcr.microsoft.com/dotnet/runtime:9.0.6-alpine3.22
LABEL com.centurylinklabs.watchtower.enable=true
WORKDIR /app
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    LC_ALL=en_US.UTF-8 \
    LANG=en_US.UTF-8
RUN apk add --no-cache git redis openssh icu-libs icu-data-full
RUN git config --global --add safe.directory /app/Lists/Private
COPY --from=build-env /app/out .
ADD Lists ./Lists
ADD config.json ./
COPY --from=git-collector /out/*.txt ./
ENTRYPOINT ["dotnet", "Cliptok.dll"]
