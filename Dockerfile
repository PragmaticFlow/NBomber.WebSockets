#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER app
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
COPY . .
WORKDIR "/examples/WebSocketsSimulator/"
RUN dotnet build "WebSocketsSimulator.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "WebSocketsSimulator.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "WebSocketsSimulator.dll"]