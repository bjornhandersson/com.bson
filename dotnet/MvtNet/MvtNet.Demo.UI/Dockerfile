FROM node:22-alpine AS build
WORKDIR /app

COPY package.json package-lock.json* ./
RUN npm install

COPY . .

ARG API_URL=http://api:8080
ENV API_URL=${API_URL}
RUN npm run build

FROM node:22-alpine
WORKDIR /app
COPY --from=build /app/.next .next
COPY --from=build /app/node_modules node_modules
COPY --from=build /app/package.json .
COPY --from=build /app/next.config.ts .

ENV API_URL=http://api:8080
EXPOSE 3000
CMD ["npm", "start"]
