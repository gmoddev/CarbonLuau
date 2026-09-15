FROM mcr.microsoft.com/dotnet/sdk:9.0-noble
ENV DEBIAN_FRONTEND=noninteractive
RUN dpkg --add-architecture i386 \
    && apt-get update \
    && apt-get install -y --no-install-recommends \
        ca-certificates cmake curl g++ lib32gcc-s1 lib32stdc++6 libatomic1 \
        libstdc++6 make mono-devel python3 python3-websocket tar \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /work
