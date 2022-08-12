FROM mcr.microsoft.com/dotnet/aspnet:3.1
EXPOSE 80/tcp
EXPOSE 443/tcp
COPY . App/
WORKDIR /App
ENTRYPOINT ["dotnet", "Squil.dll"]
