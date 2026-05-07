FROM jellyfin/jellyfin:latest

RUN apt-get update && apt-get install -y curl nodejs

RUN curl -L https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp_linux -o /usr/bin/yt-dlp && \
    chmod +x /usr/bin/yt-dlp

RUN apt-get clean && \
    rm -rf /var/lib/apt/lists/*
