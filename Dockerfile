FROM scratch
COPY --chmod=0555 Assets/StreamingAssets/Host/carousel-linux-amd64 /carousel
USER 65532:65532
EXPOSE 7777/tcp
ENTRYPOINT ["/carousel"]
