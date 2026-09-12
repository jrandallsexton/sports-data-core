# Build stage
FROM node:22-alpine AS builder

WORKDIR /app

COPY package*.json ./
RUN npm ci

COPY . .

# Optional explicit version — e.g. pass --build-arg VITE_VERSION=<image-tag>.
# Left unset it falls back to a UTC build timestamp (resolved in the build
# RUN below). Surfaced in the footer via import.meta.env.VITE_VERSION.
ARG VITE_VERSION

RUN export VITE_VERSION="${VITE_VERSION:-$(date -u +%Y.%m.%d.%H%M)}" && \
    npm run build

# Production stage — same non-root nginx pattern as src/UI/sd-ui.
FROM nginx:1.28-alpine

RUN addgroup -g 1001 -S appgroup && \
    adduser -u 1001 -S appuser -G appgroup && \
    chown -R appuser:appgroup /var/cache/nginx && \
    chown -R appuser:appgroup /var/log/nginx && \
    touch /var/run/nginx.pid && \
    chown -R appuser:appgroup /var/run/nginx.pid

COPY --chown=appuser:appgroup nginx.conf /etc/nginx/conf.d/default.conf
COPY --chown=appuser:appgroup security-headers.conf /etc/nginx/security-headers.conf

COPY --from=builder /app/build /usr/share/nginx/html

RUN chown -R appuser:appgroup /usr/share/nginx/html

USER appuser

EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
  CMD wget --no-verbose --tries=1 --spider http://localhost:8080/ || exit 1

CMD ["nginx", "-g", "daemon off;"]
