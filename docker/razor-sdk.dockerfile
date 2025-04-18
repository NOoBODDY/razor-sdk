FROM ubuntu:latest AS razor-repo

RUN apt-get update

RUN apt-get install -y wget libicu-dev

COPY ./razor /razor
WORKDIR /razor

RUN ./restore.sh