FROM ubuntu:24.04 AS prepare
ENV DEBIAN_FRONTEND=noninteractive

LABEL authors="feor"

RUN apt-get update && apt-get upgrade -y
RUN apt-get install -y build-essential clang clang-tools cmake ninja-build
RUN rm -rf /var/lib/apt/lists/*

FROM prepare AS build

WORKDIR /src
COPY . .

RUN cmake -DCMAKE_MAKE_PROGRAM=$(which ninja) -DCMAKE_CXX_COMPILER=$(which clang++) -DCMAKE_EXE_LINKER_FLAGS="-static" -DCMAKE_BUILD_TYPE=Release -G Ninja -S . -B build

RUN cmake --build build

FROM gcr.io/distroless/cc-debian12 AS run

WORKDIR /app

COPY --from=build /src/build/bitmap /app/kf_client

ENTRYPOINT ["/app/kf_client"]