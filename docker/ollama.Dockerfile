FROM ollama/ollama:latest

# Provide wget via busybox for healthchecks
# Using busybox:stable instead of busybox:stable-static (which doesn't exist)
USER root
COPY --from=busybox:stable /bin/busybox /bin/busybox
RUN ln -s /bin/busybox /usr/bin/wget

# Restore default user if image defines one (ollama runs as root by default)

