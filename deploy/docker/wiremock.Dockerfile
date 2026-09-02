FROM wiremock/wiremock:3.13.2-alpine

USER root
RUN apk add --no-cache openssl

COPY test-support/wiremock /home/wiremock
COPY deploy/docker/start-wiremock.sh /usr/local/bin/start-wiremock.sh
RUN chmod 0555 /usr/local/bin/start-wiremock.sh

EXPOSE 8080

ENTRYPOINT ["/usr/local/bin/start-wiremock.sh"]
CMD ["--port", "8080", "--global-response-templating"]
