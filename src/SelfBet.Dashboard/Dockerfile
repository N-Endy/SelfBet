FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["src/SelfBet.Domain/SelfBet.Domain.csproj",       "SelfBet.Domain/"]
COPY ["src/SelfBet.Application/SelfBet.Application.csproj", "SelfBet.Application/"]
COPY ["src/SelfBet.Infrastructure/SelfBet.Infrastructure.csproj", "SelfBet.Infrastructure/"]
COPY ["src/SelfBet.Automation/SelfBet.Automation.csproj", "SelfBet.Automation/"]
COPY ["src/SelfBet.Dashboard/SelfBet.Dashboard.csproj", "SelfBet.Dashboard/"]

RUN dotnet restore "SelfBet.Dashboard/SelfBet.Dashboard.csproj"

COPY src/SelfBet.Domain/       SelfBet.Domain/
COPY src/SelfBet.Application/  SelfBet.Application/
COPY src/SelfBet.Infrastructure/ SelfBet.Infrastructure/
COPY src/SelfBet.Automation/   SelfBet.Automation/
COPY src/SelfBet.Dashboard/    SelfBet.Dashboard/

RUN dotnet publish "SelfBet.Dashboard/SelfBet.Dashboard.csproj" -c Release -o /app/publish --no-restore

# dotnet publish on sdk:10.0.2xx sometimes skips blazor.web.js from
# Microsoft.AspNetCore.App.Internal.Assets. Copy it explicitly as a safety net.
RUN BLAZOR_JS=$(find /root/.nuget/packages -name "blazor.web.js" \
        -path "*aspnetcore.app.internal.assets*" 2>/dev/null | head -1) && \
    mkdir -p /app/publish/wwwroot/_framework && \
    if [ -n "$BLAZOR_JS" ]; then \
      cp "$BLAZOR_JS" /app/publish/wwwroot/_framework/blazor.web.js && \
      echo "Copied blazor.web.js from $BLAZOR_JS"; \
    else \
      echo "WARNING: blazor.web.js not found in NuGet cache; listing aspnetcore packages:" && \
      ls /root/.nuget/packages/ | grep -i aspnetcore || true; \
    fi

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
EXPOSE 8090
ENV ASPNETCORE_URLS=http://+:8090
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "SelfBet.Dashboard.dll"]
