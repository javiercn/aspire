FROM mcr.microsoft.com/dotnet/sdk:11.0.100-rc.1 AS build
WORKDIR /src
COPY . .
WORKDIR /src/Blazor
RUN dotnet publish "Blazor.csproj" -c Release -o /app/publish

# Prefix asset paths and add SPA fallback endpoint
RUN mkdir -p /app/output/wwwroot/app && \
    cp -r /app/publish/wwwroot/* /app/output/wwwroot/app/ && \
    dotnet run "/src/.aspire/scripts/PrefixEndpoints.cs" -- \
        /app/publish/*.staticwebassets.endpoints.json \
        app \
        /app/output/app.endpoints.json