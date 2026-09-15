FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/KyrgyzTestBot/KyrgyzTestBot.csproj src/KyrgyzTestBot/
RUN dotnet restore src/KyrgyzTestBot/KyrgyzTestBot.csproj
COPY src/ src/
RUN dotnet publish src/KyrgyzTestBot/KyrgyzTestBot.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/runtime:10.0
WORKDIR /app
COPY --from=build /app .
ENV Bot__DataDirectory=/data
VOLUME /data
ENTRYPOINT ["dotnet", "KyrgyzTestBot.dll"]
