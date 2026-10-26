# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files first so the restore layer is cached until dependencies change.
COPY DentalClinic.sln ./
COPY src/DentalClinic/DentalClinic.csproj src/DentalClinic/
COPY tests/DentalClinic.Tests/DentalClinic.Tests.csproj tests/DentalClinic.Tests/
RUN dotnet restore

COPY . .
RUN dotnet publish src/DentalClinic -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .

# The database and the cookie-encryption keys live in volumes, so they survive container restarts.
# Doctor photos uploaded in the admin area are kept in their own volume.
RUN mkdir -p /data /app/wwwroot/img/uploads && chown -R app /data /app/wwwroot/img/uploads
VOLUME /data
VOLUME /app/wwwroot/img/uploads

# The admin area stays switched off until Admin__Username and Admin__Password are set.
ENV ASPNETCORE_URLS=http://+:8080 \
    ConnectionStrings__Clinic="Data Source=/data/clinic.db" \
    DataProtection__KeysPath=/data/keys \
    HttpsRedirection=false
EXPOSE 8080

# Never run a web server as root.
USER app
ENTRYPOINT ["dotnet", "DentalClinic.dll"]
