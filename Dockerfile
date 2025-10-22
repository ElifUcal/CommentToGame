# ---------- STAGE 1: build ----------
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Sadece csproj'u kopyala (restore cache için)
COPY ./CommentToGame.csproj ./
RUN dotnet restore ./CommentToGame.csproj

# Sonra tüm kaynakları kopyala ve publish et
COPY . .
RUN dotnet publish ./CommentToGame.csproj -c Release -o /app

# ---------- STAGE 2: runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://0.0.0.0:${PORT}

COPY --from=build /app .
EXPOSE 8080
CMD ["dotnet", "CommentToGame.dll"]
