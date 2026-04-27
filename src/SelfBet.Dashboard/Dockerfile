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

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
EXPOSE 8090
ENV ASPNETCORE_URLS=http://+:8090
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "SelfBet.Dashboard.dll"]
