# use build image to compile
FROM ameier38/dotnet-mono:2.2 as builder

# prevent sending metrics to microsoft
ENV DOTNET_CLI_TELEMETRY_OPTOUT 1

WORKDIR /ouroboros

# copy paket dependencies
COPY .paket .paket
COPY paket.dependencies .
COPY paket.lock .

# copy FAKE script
COPY build.fsx .
COPY build.fsx.lock .

# copy Ouroboros
COPY src/Ouroboros/Ouroboros.fsproj src/Ouroboros/
COPY src/Ouroboros/paket.references src/Ouroboros/

# copy Ouroboros.EventStore
COPY src/Ouroboros.EventStore/Ouroboros.EventStore.fsproj src/Ouroboros.EventStore/
COPY src/Ouroboros.EventStore/paket.references src/Ouroboros.EventStore/

# copy Dog
COPY src/Dog/Dog.fsproj src/Dog/
COPY src/Dog/paket.references src/Dog/

# copy Tests
COPY src/Tests/Tests.fsproj src/Tests/
COPY src/Tests/paket.references src/Tests/

# install dependencies
RUN fake build -t Install

# copy solution
COPY Ouroboros.sln .

# restore dependencies
RUN fake build -t Restore

# copy everything else and build
COPY . .
RUN fake build -t Publish

CMD ["dotnet", "src/Tests/out/Tests.dll"]
