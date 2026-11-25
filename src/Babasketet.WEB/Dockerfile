FROM ghcr.io/cirruslabs/flutter:3.35.5 AS build
WORKDIR /app

ARG API_BASE_URL=http://localhost:8080
ARG OIDC_AUTHORITY=http://localhost:8085
ARG WILLIAM_UI_URL=http://localhost:3000

COPY pubspec.yaml ./
RUN flutter pub get

COPY . .
RUN flutter test
RUN flutter build web --release \
    --dart-define=API_BASE_URL=${API_BASE_URL} \
    --dart-define=OIDC_AUTHORITY=${OIDC_AUTHORITY} \
    --dart-define=WILLIAM_UI_URL=${WILLIAM_UI_URL}

FROM nginx:alpine AS runtime
COPY --from=build /app/build/web /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]
