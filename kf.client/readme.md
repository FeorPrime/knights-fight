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

Four executables are produced:

| Binary | Purpose |
|---|---|
| `build/kf_client` | The client itself |
| `build/test_otp` | Base32 round-trip and RFC 4226 HOTP test vectors |
| `build/test_udp` | UDP packet serialization, OTP resolution, and command validation tests |
| `build/test_config` | Configuration layering: argument keys, configuration file, defaults, and value validation |

## Usage

```shell
./build/kf_client <command> [options/arguments]
```

### Commands

- `create [challenger_name] [secret] [image_file]` — Register a challenger, optionally with an avatar (defaults: `Examinator`, `s3cret`)
- `avatar [challenger_name] [secret] <image_file>` — Replace a challenger's avatar (PNG or JPG)
- `login [challenger_name] [secret]` — Authenticate, decrypt JWT, and exercise `/test` & `/test-otp`
- `ready [challenger_name] [secret|jwt] [true|false]` — Set the challenger's ready status in the lobby (JWT protected)
- `lobby` (alias `lobby-status`) — Show the current lobby status and fighters
- `send-cmd <challenger_name> <secret|otp_seed> <command> [--host <host>] [--port <port>] [--seq <number>]` (aliases `cmd`, `udp`) — Send a game command UDP packet to the game server (port 5050)
- `--help` — Print the commands, the game-command grammar, and every configuration key

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

The endpoints and the UDP target are resolved at startup by `cfg::ConfigLoader` (`src/cfg/ConfigLoader.cpp`) and handed to the services as a filled `cfg::AppConfig`. The defaults in `include/cfg/AppConfig.h` are the local development addresses; anything else is reached through one of four layers, highest precedence first:

1. **Argument keys** — `--<key> <value>` or `--<key>=<value>`, accepted anywhere in the command line, before or after the command. The keys a command owns itself (`--host`, `--port`, `--seq`) are untouched.
2. **Environment** — `KF_CLIENT_<KEY>`, the key upper-cased with `-` replaced by `_` (`KF_CLIENT_BASE_URL`, `KF_CLIENT_UDP_PORT`, `KF_CLIENT_CONFIG`).
3. **Configuration file** — a flat JSON object of the same keys, read from `--config <file>`, from `KF_CLIENT_CONFIG`, or from `kf_client.config.json` in the working directory. The implicit file is skipped silently when absent; an explicit path that cannot be read is an error.
4. **Defaults** — `http://localhost:8080/api` and UDP `127.0.0.1:5050`.

| Key | Default value / path under `base-url` |
|---|---|
| `base-url` | `http://localhost:8080/api` — the root every endpoint below is derived from |
| `public-key-url` | `/auth/public-key` |
| `create-challenger-url` | `/challenger` |
| `login-url` | `/auth/login` |
| `test-url` | `/test` |
| `test-otp-url` | `/test-otp` |
| `lobby-url` | `/lobby` |
| `lobby-status-url` | `/lobby/status` |
| `challenger-avatar-url` | `/challengers/{id}/avatar` — the literal `{id}` is where the client substitutes the challenger id |
| `udp-host` | `127.0.0.1` |
| `udp-port` | `5050` (1–65535) |
| `config` | Configuration file path — given through arguments or the environment; a `config` entry inside the file itself is ignored |

```shell
# Point the whole client at a deployment:
./build/kf_client login Alice s3cret --base-url https://knightfight.example/api

# Move one endpoint and the UDP target, leaving the rest on the new base:
./build/kf_client login Alice s3cret --base-url https://knightfight.example/api --test-url https://knightfight.example/health
KF_CLIENT_UDP_PORT=6001 ./build/kf_client send-cmd Alice s3cret "hit_main"

# A file the whole team shares:
cat > kf_client.config.json <<'JSON'
{
  "base-url": "http://10.0.0.2:8080/api",
  "udp-port": 5051
}
JSON
./build/kf_client lobby --config ./kf_client.config.json
```

An endpoint key given literally always wins over `base-url` derivation. Unknown keys, empty values, and a non-numeric or out-of-range `udp-port` are reported (`Error: …`, exit code 1) rather than silently ignored — the full key list is printed by `--help` too.

Note the HTTP API listens on **8080**; 5050 is the UDP game protocol port. HTTP never runs on 5050.

## Tests

```shell
./build/test_otp
./build/test_config
./build/test_udp
```

`tests/test_otp.cpp`, `tests/test_config.cpp` and `tests/test_udp.cpp` are standalone executables with their own `main()`; none is registered with CTest — there is no `enable_testing()` or `add_test()` in `CMakeLists.txt`, so `ctest` finds nothing. Run them directly.

## Layout

```
include/          # public headers — cfg, crypto, http, models, services, udp, cli
src/
├── cfg/          # AppConfig defaults and the layered configuration loader
├── cli/          # the command line front end
├── crypto/       # OpenSSL RSA encryptor, AES-GCM decryptor, Base32/Base64, OTP, key manager
├── http/         # cpr-backed HTTP client behind an interface
├── services/     # AuthService, ChallengerService, KeyService, AvatarService, UdpCommandService
├── udp/          # the UDP game-protocol client
└── models/       # request/response models
tests/            # test_otp.cpp, test_udp.cpp, test_config.cpp
```

The HTTP client sits behind `http::IHttpClient` and the UDP client behind `udp::IUdpClient`, so the services can be exercised without a live server.

## Known issues

- **`test_udp` depends on the wall clock.** `services::UdpCommandService::createPacket` generates the TOTP from the current time and ignores the packet timestamp it is handed, so `tests/test_udp.cpp` asserts the RFC 6238 code for T = 59 s and fails once the clock leaves that step. This is unrelated to configuration — `test_otp` and `test_config` are deterministic.
