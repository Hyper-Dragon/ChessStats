# Appliance image: the published single-file ChessStats binary + nginx + a refresh loop.
#
# Nothing is compiled here. The workflow downloads the release asset and copies it in, so the
# image always contains exactly the bytes that were published and can be rebuilt without a
# toolchain (for example after an nginx config change).
FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-noble

# Path to the extracted release binary, relative to the build context.
ARG BINARY=publish/ChessStats

RUN apt-get update \
    && apt-get install -y --no-install-recommends nginx ca-certificates tini \
    && rm -rf /var/lib/apt/lists/* \
    && rm -f /etc/nginx/sites-enabled/default

WORKDIR /app

COPY ${BINARY} /app/chess-stats
RUN chmod +x /app/chess-stats

COPY container/nginx.conf /etc/nginx/conf.d/default.conf
COPY container/run.sh /app/run.sh
RUN chmod +x /app/run.sh

# ChessStats writes its reports and cache next to the executable, which is /app.
VOLUME ["/app/ChessStatsResults", "/app/ChessStatsCache"]
EXPOSE 80

# tini is PID 1 so docker stop reaches nginx and the refresh loop promptly.
ENTRYPOINT ["/usr/bin/tini", "--", "/app/run.sh"]
