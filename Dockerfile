FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["Commute360.csproj", "./"]
RUN dotnet restore "Commute360.csproj"
COPY . .
RUN dotnet publish "Commute360.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
RUN apt-get update && apt-get install -y libgssapi-krb5-2 && rm -rf /var/lib/apt/lists/*
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Commute360.dll"]