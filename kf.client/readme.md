# kf.client

C++23 desktop client for KnightFight.

This is the **only** component that implements the full encrypted auth flow: it generates an RSA keypair, encrypts payloads with the server's public key, decrypts the returned JWT + OTP seed, and validates a TOTP code. The web UI deliberately does none of this — it calls anonymous endpoints only.

## Requirements

- A C++23 compiler, CMake ≥ 3.28 (`CMakeLists.txt` asks for it), and any generator CMake supports — the project itself is platform-neutral (`if(MSVC) /W4 … else() -Wall -Wextra -Wpedantic`).
- `cpr` 1.10.5 and `nlohmann_json` 3.11.3 are fetched by `cmake/FetchDeps.cmake`, so **network is needed for the first configure** (`git clone` of both). OpenSSL is a hard dependency of this project (`find_package(OpenSSL REQUIRED)` runs before the fetch).
- cURL comes from the host by default (`set(CPR_USE_SYSTEM_CURL ON)`), which on Linux means the `-dev` packages must be installed. Override it with **`-DCPR_USE_SYSTEM_CURL=OFF`** to have cpr build its own cURL instead — then only OpenSSL headers/libs are needed (see the WSL section).

### Windows (MSVC + vcpkg)

```shell
vcpkg install curl openssl nlohmann-json
```

cpr and nlohmann/json come from `cmake/FetchDeps.cmake`, not from vcpkg — `curl` and `openssl` are the two the configure step actually resolves through the toolchain file.

The vcpkg toolchain file is what makes `find_package(OpenSSL)` and `find_package(CURL)` resolve, so pass it on the configure line:

```shell
cmake -S . -B build-win -DCMAKE_TOOLCHAIN_FILE=<vcpkg>/scripts/buildsystems/vcpkg.cmake
cmake --build build-win --config Debug --parallel 2
cmake --build build-win --config Release --parallel 2
```

Keep `--parallel` at 2 on this machine: at 8 the MSVC compiler runs out of heap (`C1060`) while building the in-tree cpr/OpenSSL sources.

The default generator here is Visual Studio (multi-config), so the binaries land under `build-win\Debug\`:

```
build-win\Debug\kf_client.exe  test_otp.exe  test_udp.exe  test_config.exe
```

**The one Windows-only trap: two different cpr builds are on disk at once.** The project builds cpr itself, so its headers, its import library and its `cpr.dll` all come from one source tree (`build-win\_deps\cpr-build\cpr\<Config>\`). vcpkg is a second supplier of the same name: its post-link step (`z-applocal`) copies *its* `cpr.dll` next to `kf_client.exe`, and the Windows loader — which resolves imports by base name, looking beside the executable first — opens that one instead. Two independent builds of a C++ library do not agree on their export tables, so names our own objects promised (cpr's `Parameters(std::initializer_list<Parameter>)` constructor, declared in `parameters.h` and defined in cpr's `parameters.cpp`, instantiated from our translation units) can be absent from the DLL the loader actually opened: the link succeeds against `cpr.lib`, and the image dies before `main()` with `0xC0000139` (`STATUS_ENTRYPOINT_NOT_FOUND`, Win32 error 127 "import not found in the export section") — writing nothing and exiting `-1073741511`. Debug escaped by luck: the optimiser keeps that reference in Release and Debug's codegen happened to ask only for names vcpkg's table also had, which is why one configuration ran and the other one never did.

`CMakeLists.txt` fixes that for every configuration with a post-link step, `cmake/AlignCprRuntime.cmake`: after applocal has had its turn, it overwrites the `cpr.dll` beside the executable with the one this project built, so headers, import library and DLL are the same build. vcpkg's `cpr` package is not needed here for that reason — installing it is now harmless, but it is what puts a second `cpr.dll` in the loader's reach.

Cross-check it with `dumpbin /IMPORTS kf_client.exe` against `dumpbin /EXPORTS` of the `cpr.dll` that sits next to the executable (`/HEADERS` prints the build id, and a name in the first list but not in the second is a broken import). A mismatch between that DLL and `build-win\_deps\cpr-build\cpr\<Config>\cpr.dll` — compare their SHA-256 — is the same defect seen from the other side.

Also note that the incremental link (`/INCREMENTAL /ILK:"kf_client.dir\Debug\kf_client.ilk"`) keeps the previous import table, so an obsolete-but-unresolvable import survives until you force a full link: delete the `.ilk` (or the `.exe`) and rebuild, or configure a fresh tree. Without that, a correct source fix can look like it changed nothing.

`cmake-build-debug-visual-studio\`, `out\build\x64-Debug\` and `build\` are earlier build directories of the same configuration; any of them can be reused instead of a new one.

### Linux (GCC or Clang + Ninja)

```shell
sudo apt install libcurl4-openssl-dev libssl-dev   # headers + libs for the default system-curl path
cmake -S . -B build -G Ninja
cmake --build build --parallel 8
```

Single-config, so the binaries are `build/kf_client`, `build/test_otp`, …

### WSL / minimal Linux without `-dev` packages

WSL's base image has the runtime libraries (`libcurl.so.4`, `libssl.so.3`, `libz.so`) and `cmake`/`gcc`/`ninja`/`git`, but **not the development headers**, and there is usually no passwordless `sudo`. Two ways out:

- Unpack the matching `-dev` package into a private prefix — no root needed. The Ubuntu pool is keyed by *source* package, so the paths are `pool/main/c/curl`, `main/o/openssl`, `main/z/zlib` (guessing `main/l/libssl-dev` 404s), and Ubuntu 24.04 puts the headers in the multiarch dir: `<prefix>/usr/include/x86_64-linux-gnu/curl/curl.h`, not `<prefix>/usr/include/curl/`.

  ```shell
  curl -sLO http://archive.ubuntu.com/ubuntu/pool/main/c/curl/libcurl4-openssl-dev_8.5.0-2ubuntu10.16_amd64.deb
  dpkg -x libcurl4-openssl-dev_*.deb /tmp/kfdev     # also ships curl-config + .pc files
  ```

  Then point CMake at it:

  ```shell
  cmake -S . -B build -G Ninja \
    -DCMAKE_CXX_COMPILER=g++-14 \
    -DCMAKE_CXX_FLAGS="-isystem /tmp/kfdev/usr/include -isystem /tmp/kfdev/usr/include/x86_64-linux-gnu" \
    -DOPENSSL_INCLUDE_DIR=/tmp/kfdev/usr/include \
    -DOPENSSL_SSL_LIBRARY=/usr/lib/x86_64-linux-gnu/libssl.so.3 \
    -DOPENSSL_CRYPTO_LIBRARY=/usr/lib/x86_64-linux-gnu/libcrypto.so.3 \
    -DCURL_INCLUDE_DIR=/tmp/kfdev/usr/include/x86_64-linux-gnu/curl \
    -DCURL_LIBRARY=/usr/lib/x86_64-linux-gnu/libcurl.so.4
  ```

- Or let cpr build its own cURL (`-DCPR_USE_SYSTEM_CURL=OFF`) and supply only OpenSSL headers. That fetched cURL is built from **C** sources, so repeat the include paths in `-DCMAKE_C_FLAGS` — with only `CMAKE_CXX_FLAGS` set, the bundled cURL fails on `openssl/opensslconf.h: No such file or directory`:

  ```shell
  cmake -S . -B build -G Ninja \
    -DCMAKE_CXX_FLAGS="-isystem /tmp/kfdev/usr/include -isystem /tmp/kfdev/usr/include/x86_64-linux-gnu" \
    -DCMAKE_C_FLAGS="-isystem /tmp/kfdev/usr/include -isystem /tmp/kfdev/usr/include/x86_64-linux-gnu" \
    -DOPENSSL_INCLUDE_DIR=/tmp/kfdev/usr/include \
    -DOPENSSL_SSL_LIBRARY=/usr/lib/x86_64-linux-gnu/libssl.so.3 \
    -DOPENSSL_CRYPTO_LIBRARY=/usr/lib/x86_64-linux-gnu/libcrypto.so.3 \
    -DCPR_USE_SYSTEM_CURL=OFF
  ```

**Do not carry the Windows `PATH` into a WSL build.** WSL appends the Windows `PATH` by default, and if it contains a vcpkg tree (`…\installed\x64-windows\{lib,bin,include}` is commonly on it), CMake resolves `find_package(CURL)` to the **Windows** `CURLConfig.cmake` — configure then reports `Found CURL: …/x64-windows/share/curl/CURLConfig.cmake` and the Linux link line gets `…\debug\bin\libcurl-d.dll`, which `ld` rejects with `file format not recognized`. Strip the `/mnt/...` entries from `PATH` (or pass `-DCURL_FOUND=TRUE` with the system `CURL_INCLUDE_DIR`/`CURL_LIBRARY`) and the same configure picks the native library.

Both builds run: `build/kf_client --help` (native) and `build-win/Debug/kf_client.exe --help` (MSVC) print the usage and exit 0, and `test_otp`/`test_config` pass. Building `--config Release` gives the same output from `build-win/Release/kf_client.exe` — the post-link step in `CMakeLists.txt` is what keeps the two configurations loading the same cpr. Keep the two in sync if you change one of them.

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

- **Fixed: `kf_client.exe` was unstartable on Windows** (it wrote nothing and exited `0xC0000139` / `-1073741511`, also with `--help`, in the Ninja `build\`, the VS `out\build\x64-Debug\`, `cmake-build-debug-visual-studio\` and a fresh `build-win\`). The source was never at fault: two unrelated cpr builds coexist on Windows, and vcpkg's post-link step put its own `cpr.dll` beside the executable — the loader resolves by base name and opens that one, whose export table is missing names our objects demanded (cpr's `Parameters(std::initializer_list<Parameter>)` constructor, demanded in Release, which Debug's codegen happened not to ask for: one configuration ran, the other did not). `cmake/AlignCprRuntime.cmake`, run as a post-link step from `CMakeLists.txt`, puts the cpr this project built back there; `src/cli/CliApp.cpp` is unchanged. The incremental link kept the old import table while an earlier source-level attempt was being tested, so that attempt looked like it changed nothing until the `.ilk` was dropped — see the "Windows (MSVC + vcpkg)" section.
- **Windows warnings that `/W4` keeps loud:** `C4996` on `getenv` in `src/cfg/ConfigLoader.cpp:95`, `C4244` narrowing `__int64`→`long` inside `std::expected<long, std::string>` instantiated from `src/crypto/JwtPayload.cpp:30`, and `C4101` unreferenced local in `src/cli/CliApp.cpp:425`. They are not fatal and the link succeeds; treat them as separate clean-up work, not as the startup defect above.
- **`test_udp` depends on the wall clock.** `services::UdpCommandService::createPacket` generates the TOTP from the current time and ignores the packet timestamp it is handed, so `tests/test_udp.cpp` asserts the RFC 6238 code for T = 59 s and fails once the clock leaves that step. This is unrelated to configuration — `test_otp` and `test_config` are deterministic.
