FROM node:24-alpine AS build
WORKDIR /app

COPY package.json package-lock.json ./
RUN npm ci --no-audit --no-fund

COPY . .
ARG VITE_API_BASE_URL=
ARG VITE_USE_MOCKS=false
ARG VITE_OIDC_AUTHORITY=
ARG VITE_OIDC_CLIENT_ID=
ARG VITE_OIDC_SCOPE="openid profile"
ARG VITE_OIDC_ALLOW_HTTP_LOCAL=false
ARG VITE_OIDC_ALLOW_HTTP_QA=false
ENV VITE_OIDC_AUTHORITY=${VITE_OIDC_AUTHORITY}
ENV VITE_OIDC_CLIENT_ID=${VITE_OIDC_CLIENT_ID}
ENV VITE_OIDC_SCOPE=${VITE_OIDC_SCOPE}
ENV VITE_OIDC_ALLOW_HTTP_LOCAL=${VITE_OIDC_ALLOW_HTTP_LOCAL}
ENV VITE_OIDC_ALLOW_HTTP_QA=${VITE_OIDC_ALLOW_HTTP_QA}
ENV VITE_API_BASE_URL=${VITE_API_BASE_URL}
ENV VITE_USE_MOCKS=${VITE_USE_MOCKS}
RUN npm run build

FROM nginx:stable-alpine AS final
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html

# QA #553: los assets publicos pueden llegar al build con modo 0600.
# Nginx sirve ficheros como usuario no-root; evitar 403 sin ampliar secretos
# ni cambiar permisos de otras rutas de la imagen.
RUN chmod 0755 /usr/share/nginx/html/brand \
    && find /usr/share/nginx/html/brand -maxdepth 1 -type f -exec chmod 0644 {} + \
    && test -r /usr/share/nginx/html/brand/logo-glmch-reducido-v2-azul.svg \
    && test -r /usr/share/nginx/html/brand/favicon-glmch.svg \
    && test -r /usr/share/nginx/html/brand/favicon-32.png \
    && test -r /usr/share/nginx/html/brand/apple-touch-icon.png

EXPOSE 80
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
  CMD wget -qO- http://127.0.0.1/health/web >/dev/null || exit 1
