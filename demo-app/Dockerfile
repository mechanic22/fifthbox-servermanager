FROM node:22-alpine

WORKDIR /app
COPY server.mjs ./

ENV PORT=8080 \
    DEMO_TITLE="FifthBox demo app" \
    DEMO_MESSAGE="Set DEMO_MESSAGE at deploy time to change this text." \
    DEMO_COLOR="#6750a4"

EXPOSE 8080
USER node

HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD node -e "fetch('http://127.0.0.1:'+(process.env.PORT||8080)+'/healthz').then(r=>process.exit(r.ok?0:1)).catch(()=>process.exit(1))"

ENTRYPOINT ["node", "server.mjs"]
