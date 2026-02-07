# Build stage
FROM node:20.18-alpine AS builder

WORKDIR /app

# Copy package files
COPY package*.json ./

# Install all dependencies (including devDependencies needed for build)
RUN npm ci

# Copy source code
COPY . .

# Accept build arguments for React environment variables
ARG REACT_APP_API_BASE_URL
ARG REACT_APP_ENVIRONMENT
ARG REACT_APP_PROD_URL
ARG REACT_APP_GOOGLE_MAPS_API_KEY

# Set as environment variables for the build
ENV REACT_APP_API_BASE_URL=${REACT_APP_API_BASE_URL}
ENV REACT_APP_ENVIRONMENT=${REACT_APP_ENVIRONMENT}
ENV REACT_APP_PROD_URL=${REACT_APP_PROD_URL}
ENV REACT_APP_GOOGLE_MAPS_API_KEY=${REACT_APP_GOOGLE_MAPS_API_KEY}

# Build the app
RUN npm run build

# Production stage
FROM nginx:1.28-alpine

# Create non-root user
RUN addgroup -g 1001 -S appgroup && \
    adduser -u 1001 -S appuser -G appgroup && \
    chown -R appuser:appgroup /var/cache/nginx && \
    chown -R appuser:appgroup /var/log/nginx && \
    touch /var/run/nginx.pid && \
    chown -R appuser:appgroup /var/run/nginx.pid

# Copy custom nginx config with proper ownership
COPY --chown=appuser:appgroup nginx.conf /etc/nginx/conf.d/default.conf
COPY --chown=appuser:appgroup security-headers.conf /etc/nginx/security-headers.conf

# Copy built app from builder stage
COPY --from=builder /app/build /usr/share/nginx/html

# Ensure appuser owns the html directory
RUN chown -R appuser:appgroup /usr/share/nginx/html

# Switch to non-root user
USER appuser

# Expose unprivileged port 8080
EXPOSE 8080

# Add healthcheck
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
  CMD wget --no-verbose --tries=1 --spider http://localhost:8080/ || exit 1

# Start nginx
CMD ["nginx", "-g", "daemon off;"]
