# ---------- STAGE 1: build ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Sadece csproj'ları kopyala ki restore layer'ı cache'lensin
COPY *.sln .
COPY CommentToGame/*.csproj ./CommentToGame/
# (Başka classlib projelerin varsa aynı şekilde ekle)
# COPY SomeLib/SomeLib.csproj ./SomeLib/

RUN dotnet restore

# Şimdi tüm kaynak kodu kopyala ve publish et
COPY . .
RUN dotnet publish CommentToGame/CommentToGame.csproj -c Release -o /app

# ---------- STAGE 2: runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

# Render $PORT verir; 0.0.0.0'a bind et
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://0.0.0.0:${PORT}

COPY --from=build /app .
# (dll adı proje adın: CommentToGame.dll)
EXPOSE 8080
CMD ["dotnet", "CommentToGame.dll"]
