FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# نسخ ملفات المشروع واستعادة التبعيات
COPY *.csproj ./
RUN dotnet restore

# نسخ بقية الملفات والبناء
COPY . ./
RUN dotnet publish -c Release -o out

# إنشاء صورة التشغيل
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/out ./
ENTRYPOINT ["dotnet", "reactBackend.dll"]