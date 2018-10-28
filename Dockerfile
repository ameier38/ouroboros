# use build image to compile
FROM ameier38/dotnet-mono as builder

# prevent sending metrics to microsoft
ENV DOTNET_CLI_TELEMETRY_OPTOUT 1

WORKDIR /ouroboros

# copy paket dependencies
COPY paket.dependencies .
COPY paket.lock .

# copy Ouroboros
COPY src/Ouroboros/Ouroboros.fsproj src/Ouroboros/
COPY src/Ouroboros/paket.references src/Ouroboros/

# copy Tests
COPY src/Tests/Tests.fsproj src/Tests/
COPY src/Tests/paket.references src/Tests/

# install dependencies
COPY .paket .paket
RUN mono .paket/paket.exe install

# copy solution
COPY Ouroboros.sln .

# restore dependencies
RUN dotnet restore

# copy everything else and build
COPY . .
RUN dotnet publish src/Tests/Tests.fsproj -c Release -o out

# use runtime image for final image
FROM microsoft/dotnet:2.1-runtime

WORKDIR /ouroboros
COPY --from=builder /ouroboros/src/Tests/out .

CMD ["dotnet", "Tests.dll"]
