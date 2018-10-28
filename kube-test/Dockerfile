FROM ameier38/ouroboros-test

# install OpenFaas function watchdog
RUN apt-get update -qy \
    && apt-get install -qy curl ca-certificates --no-install-recommends \
    && echo "Pulling watchdog binary from Github." \
    && curl -sSL https://github.com/openfaas/faas/releases/download/0.9.6/fwatchdog > /usr/bin/fwatchdog \
    && chmod +x /usr/bin/fwatchdog \
    && apt-get -qy remove curl \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/*
    
ENV fprocess="dotnet /ouroboros/Tests.dll"

EXPOSE 8080

HEALTHCHECK --interval=3s CMD [ -e /tmp/.lock ] || exit 1
CMD [ "fwatchdog" ]
