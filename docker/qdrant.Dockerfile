FROM qdrant/qdrant:latest

# Install curl for robust HTTP healthchecks (handles Debian/Alpine)
USER root
RUN (command -v apt-get >/dev/null \
     && apt-get update \
     && apt-get install -y --no-install-recommends curl ca-certificates \
     && rm -rf /var/lib/apt/lists/*) \
  || (command -v apk >/dev/null \
     && apk add --no-cache curl ca-certificates) \
  || (echo "No supported package manager found in qdrant base image" && exit 1)

