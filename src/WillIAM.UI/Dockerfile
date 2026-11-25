FROM node:22-alpine AS build
WORKDIR /app
COPY package*.json ./
RUN npm install
COPY . .
RUN npm run check && npm run test && npm run build

FROM node:22-alpine AS runtime
WORKDIR /app
RUN apk add --no-cache wget
ENV NODE_ENV=production
COPY --from=build /app/build ./build
COPY --from=build /app/package.json ./
COPY --from=build /app/node_modules ./node_modules
EXPOSE 3000
CMD ["node", "build"]
