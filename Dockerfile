# use build image to compile
FROM ameier38/dotnet-mono as builder

# prevent sending metrics to microsoft
ENV DOTNET_CLI_TELEMETRY_OPTOUT 1

WORKDIR /ouroboros

# copy paket dependencies
COPY paket.dependencies .
COPY paket.lock .

# copy Common
COPY src/Common/Common.fsproj src/Common/

# copy Ouroboros
COPY src/EventSourcing/EventSourcing.fsproj src/EventSourcing/
COPY src/EventSourcing/paket.references src/EventSourcing/

# copy Sales
COPY src/Sales/Sales.fsproj src/Sales/
COPY src/Sales/paket.references src/Sales/

# install piper dependencies
COPY .paket .paket
RUN mono .paket/paket.exe install
RUN dotnet restore src/Sales/Sales.fsproj

# copy everything else and build
COPY . .
RUN dotnet publish src/Sales/Sales.fsproj -c Release -o out

# use runtime image for final image
FROM microsoft/dotnet:2.1-runtime

WORKDIR /simba
COPY --from=builder /simba/src/Sales/out out
COPY --from=builder /usr/bin/fwatchdog .
