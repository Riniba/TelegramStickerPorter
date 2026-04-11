FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-noble
WORKDIR /app
ARG BIN_NAME=TelegramStickerPorter
ARG TARGETARCH
COPY out/linux-${TARGETARCH}/ /app/
RUN chmod +x /app/${BIN_NAME}
EXPOSE 5005
ENTRYPOINT ["/app/TelegramStickerPorter"]
