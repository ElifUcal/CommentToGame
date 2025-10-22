# ---------- STAGE 1: build ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Önce sadece csproj'u kopyala (restore cache için)
COPY ./CommentToGame.csproj ./
RUN dotnet restore ./CommentToGame.csproj

# Sonra tüm kaynakları kopyala ve publish et
COPY . .
RUN dotnet publish ./CommentToGame.csproj -c Release -o /app

# ---------- STAGE 2: runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

# Render PORT verir; 0.0.0.0'a bind et
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://0.0.0.0:${PORT}

COPY --from=build /app .
EXPOSE 8080

# Proje adınla aynı isimde dll üretilecek: CommentToGame.dll
CMD ["dotnet", "CommentToGame.dll"]
