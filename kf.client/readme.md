# kf.client

C++23 desktop client for KnightFight.

This is the **only** component that implements the full encrypted auth flow: it generates an RSA keypair, encrypts payloads with the server's public key, decrypts the returned JWT + OTP seed, and validates a TOTP code. The web UI deliberately does none of this — it calls anonymous endpoints only.

## Requirements

- A C++23 compiler with module support, CMake ≥ 3.28, and a build system that supports modules
- vcpkg dependencies:

```shell
vcpkg install curl openssl cpr nlohmann-json
```

On Linux:

```shell
apt install libcurl4-openssl-dev libssl-dev
```

On macOS with Homebrew:

```shell
brew install cmake ninja clang
```

## Build

```shell
cd kf.client
cmake -DCMAKE_MAKE_PROGRAM=$(which ninja) -DCMAKE_CXX_COMPILER=$(which clang++) -G Ninja -S . -B build
cmake --build build -j
```

Three executables are produced:

| Binary | Purpose |
|---|---|
| `build/kf_client` | The client itself |
| `build/test_otp` | Base32 round-trip and RFC 4226 HOTP test vectors |
| `build/test_udp` | UDP packet serialization, OTP resolution, and command validation tests |

## Usage

```shell
./build/kf_client <command> [options/arguments]
```

### Commands

- `create [challenger_name] [secret]` — Register a challenger (defaults: `Examinator`, `s3cret`)
- `login [challenger_name] [secret]` — Authenticate, decrypt JWT, and exercise `/test` & `/test-otp`
- `send-cmd <challenger_name> <secret|otp_seed> <command> [--host <host>] [--port <port>] [--seq <number>]` — Send a game command UDP packet to the game server (port 5050)

### `create` — register a challenger

```shell
./build/kf_client create Alice s3cret
```

Fetches the server's public key, encrypts the name and secret with it (RSA-OAEP-SHA256), and posts the sealed envelope to `POST /api/challenger`. The secret is hashed with SHA3-512 server-side before storage; it is never stored in the clear.

### `login` — authenticate and exercise the protected endpoints

```shell
./build/kf_client login Alice s3cret
```

1. Generates a client RSA keypair
2. Posts an encrypted login envelope to `POST /api/Auth/login`
3. Decrypts the response with the client private key, yielding a JWT and an OTP seed
4. Calls `GET /api/test` with `Authorization: Bearer <jwt>`
5. Generates a TOTP code from the seed and calls `GET /api/test-otp`

### `send-cmd` (aliases: `cmd`, `udp`) — send game command UDP packet

```shell
# Authenticate with secret and send command:
./build/kf_client send-cmd Alice s3cret "hit_main"

# Send directly with known Base32 OTP seed or 6-digit OTP code:
./build/kf_client send-cmd Alice JBSWY3DPEHPK3PXP "turn right" --port 5050

# Custom sequence number and target host:
./build/kf_client send-cmd Alice s3cret "move" --host 127.0.0.1 --port 5050 --seq 42
```

Supported standard game commands (case-insensitive):
- `move`
- `turn left`, `turn right`
- `hit_main`, `hit_off`

## Configuration

Endpoints are hardcoded in `include/cfg/AppConfig.h`:

```cpp
const std::string BASE_URL {"http://localhost:8080/api"};
```

| Key | Resulting URL |
|---|---|
| `publicKeyUrl` | `/api/auth/public-key` |
| `createChallengerUrl` | `/api/challenger` |
| `loginUrl` | `/api/auth/login` |
| `testUrl` | `/api/test` |
| `testOtpUrl` | `/api/test-otp` |

`BASE_URL` is not overridable from the environment or a config file — change it and rebuild. Note the HTTP API listens on **8080**; 5050 is the planned UDP game protocol port.

## Tests

```shell
./build/test_otp
```

`tests/test_otp.cpp` is a standalone executable that asserts Base32 round-tripping and the RFC 4226 HOTP test vectors. It is **not** registered with CTest — there is no `enable_testing()` or `add_test()` in `CMakeLists.txt`, so `ctest` will not pick it up.

## Layout

```
include/          # public headers — cfg, crypto, http, models, services
src/
├── crypto/       # OpenSSL RSA encryptor, AES-GCM decryptor, Base32/Base64, OTP, key manager
├── http/         # cpr-backed HTTP client behind an interface
├── services/     # AuthService, ChallengerService, KeyService
└── models/       # request/response models
tests/            # test_otp.cpp
```

The HTTP client sits behind `http::IHttpClient` so the services can be exercised without a live server.

## Known issues

- **`BASE_URL` is hardcoded**, so the client cannot be pointed at a deployed environment without a rebuild.
