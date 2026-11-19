#include "cli/CliApp.h"

#include "crypto/ClientKeyManager.h"
#include "crypto/JwtPayload.h"
#include "crypto/OpensslRsaEncryptor.h"
#include "crypto/OtpService.h"
#include "http/CprHttpClient.h"
#include "services/AuthService.h"
#include "services/AvatarService.h"
#include "services/ChallengerService.h"
#include "services/KeyService.h"
#include "services/UdpCommandService.h"
#include "udp/UdpClient.h"

#include <algorithm>
#include <cpr/cpr.h>
#include <format>
#include <iostream>
#include <memory>
#include <nlohmann/json.hpp>

namespace cli {

    CliApp::CliApp(cfg::AppConfig cfg)
        : cfg_(std::move(cfg)) {}

    void CliApp::printUsage(std::string_view programName) const {
        std::cerr << std::format("Usage: {} <command> [options/arguments]\n\n", programName);
        std::cerr << "Commands:\n";
        std::cerr << "  create   [name] [secret] [image_file]        Create a new challenger, optionally with an avatar\n";
        std::cerr << "  avatar   <name> <secret> <image_file>        Replace a challenger's avatar (PNG or JPG)\n";
        std::cerr << "  login    [name] [secret]                     Authenticate and test protected endpoints\n";
        std::cerr << "  ready    <name> <secret|jwt> [true|false]    Set challenger ready status in lobby (JWT protected)\n";
        std::cerr << "  lobby                                        View current lobby status and fighters\n";
        std::cerr << "  send-cmd <name> <secret|otp_seed> <command>  Send a game command UDP packet\n";
        std::cerr << "           [--host <host>] [--port <port>] [--seq <number>]\n\n";
        std::cerr << "Game commands (case-insensitive):\n";
        std::cerr << "  move\n";
        std::cerr << "  turn left | turn right\n";
        std::cerr << "  hit_main | hit_off\n\n";
        std::cerr << "Configuration, highest precedence first:\n";
        std::cerr << "  1. Arguments: --base-url <url> --udp-host <host> --udp-port <port> --config <file>,\n";
        std::cerr << "     one per endpoint too (--login-url, --test-url, --challenger-avatar-url, ...).\n";
        std::cerr << "     Works with any command, before or after it.\n";
        std::cerr << "  2. Environment: KF_CLIENT_<KEY> for the same keys (KF_CLIENT_BASE_URL, KF_CLIENT_UDP_PORT).\n";
        std::cerr << "  3. Configuration file: a JSON object of the same keys, read from --config, from\n";
        std::cerr << "     KF_CLIENT_CONFIG, or from kf_client.config.json in the working directory.\n";
        std::cerr << "  4. Defaults: the local development addresses - http://localhost:8080/api, 127.0.0.1:5050.\n\n";
        std::cerr << "Examples:\n";
        std::cerr << std::format("  {} create Alice s3cret\n", programName);
        std::cerr << std::format("  {} create Alice s3cret ./my-avatar.png\n", programName);
        std::cerr << std::format("  {} avatar Alice s3cret ./my-avatar.png\n", programName);
        std::cerr << std::format("  {} login Alice s3cret\n", programName);
        std::cerr << std::format("  {} ready Alice s3cret true\n", programName);
        std::cerr << std::format("  {} lobby\n", programName);
        std::cerr << std::format("  {} send-cmd Alice s3cret \"hit_main\"\n", programName);
        std::cerr << std::format("  {} send-cmd Alice JBSWY3DPEHPK3PXP \"turn right\" --port 5050\n", programName);
        std::cerr << std::format("  {} lobby --base-url http://10.0.0.2:8080/api\n", programName);
    }

    int CliApp::run(int argc, char* argv[]) {
        if (argc < 2) {
            printUsage(argv[0]);
            return 1;
        }

        std::vector<std::string_view> args;
        args.reserve(argc);
        for (int i = 0; i < argc; ++i) {
            args.emplace_back(argv[i]);
        }

        return run(std::span<const std::string_view>{args});
    }

    int CliApp::run(std::span<const std::string_view> args) {
        if (args.size() < 2) {
            printUsage(args.empty() ? "kf_client" : args[0]);
            return 1;
        }

        std::string_view command = args[1];
        if (command == "-h" || command == "--help" || command == "help") {
            printUsage(args[0]);
            return 0;
        }

        std::vector<std::string> cmdArgs;
        cmdArgs.reserve(args.size() - 2);
        for (size_t i = 2; i < args.size(); ++i) {
            cmdArgs.emplace_back(args[i]);
        }

        if (command == "create") {
            return executeCreate(cmdArgs);
        }
        if (command == "avatar") {
            return executeAvatar(cmdArgs);
        }
        if (command == "login") {
            return executeLogin(cmdArgs);
        }
        if (command == "ready") {
            return executeReady(cmdArgs);
        }
        if (command == "lobby" || command == "lobby-status") {
            return executeLobby(cmdArgs);
        }
        if (command == "send-cmd" || command == "cmd" || command == "udp") {
            return executeSendCmd(cmdArgs);
        }

        std::cerr << std::format("Unknown command: '{}'\n\n", command);
        printUsage(args[0]);
        return 1;
    }

    std::expected<AuthenticatedChallenger, std::string>
    CliApp::authenticate(const std::string& name, const std::string& secret) {
        auto http = std::make_shared<http::CprHttpClient>();
        auto serverEncryptor = std::make_shared<crypto::OpensslRsaEncryptor>();
        auto clientKeyManager = std::make_shared<crypto::ClientKeyManager>();
        auto keyService = std::make_shared<services::KeyService>(http, serverEncryptor, cfg_);
        services::AuthService auth(http, serverEncryptor, clientKeyManager, keyService, cfg_);

        if (auto r = clientKeyManager->generate(); !r) {
            return std::unexpected(std::format("Failed to generate client keys: {}", r.error()));
        }

        auto loginResult = auth.login(name, secret);
        if (!loginResult) {
            return std::unexpected(std::format("Login failed: {}", loginResult.error()));
        }

        const auto& login = loginResult.value();

        auto id = crypto::extractSubject(login.jwt);
        if (!id) {
            return std::unexpected(std::format("Cannot read the challenger id: {}", id.error()));
        }

        return AuthenticatedChallenger{*id, login.jwt};
    }

    int CliApp::executeAvatar(const std::vector<std::string>& args) {
        std::string name = args.size() > 0 ? args[0] : "Examinator";
        std::string secret = args.size() > 1 ? args[1] : "s3cret";
        std::string avatarPath = args.size() > 2 ? args[2] : "";

        if (avatarPath.empty()) {
            std::cerr << "Error: avatar requires <name> <secret> <image_file>\n";
            std::cerr << "Example: kf_client avatar Alice s3cret ./my-avatar.png\n";
            return 1;
        }

        std::cout << "1. Authenticating as '" << name << "'...\n";
        auto auth = authenticate(name, secret);
        if (!auth) {
            std::cerr << std::format("   [!] {}\n", auth.error());
            return 1;
        }
        std::cout << "   [✓] Authenticated.\n";

        auto http = std::make_shared<http::CprHttpClient>();
        services::AvatarService avatars(http, cfg_);

        std::cout << std::format("2. Uploading avatar '{}'...\n", avatarPath);
        auto uploaded = avatars.uploadAvatar(auth->id, avatarPath, auth->jwt);
        if (!uploaded) {
            std::cerr << std::format("   [!] Avatar upload failed: {}\n", uploaded.error());
            return 1;
        }

        std::cout << "   [✓] Avatar uploaded.\n";
        std::cout << "Server response:\n" << uploaded.value() << '\n';
        return 0;
    }

    int CliApp::executeCreate(const std::vector<std::string>& args) {
        std::string name = args.size() > 0 ? args[0] : "Examinator";
        std::string secret = args.size() > 1 ? args[1] : "s3cret";
        std::string avatarPath = args.size() > 2 ? args[2] : "";

        auto http = std::make_shared<http::CprHttpClient>();
        auto serverEncryptor = std::make_shared<crypto::OpensslRsaEncryptor>();
        auto keyService = std::make_shared<services::KeyService>(http, serverEncryptor, cfg_);
        services::ChallengerService challengers(http, serverEncryptor, cfg_);

        std::cout << "1. Fetching server public key...\n";
        if (auto r = keyService->ensureLoaded(); !r) {
            std::cerr << std::format("   [!] Failed to load key: {}\n", r.error());
            return 1;
        }
        std::cout << "   [✓] Server key loaded.\n";

        models::ChallengerRequest req{.name = name, .secret = secret};

        std::cout << std::format("2. Creating challenger '{}'...\n", req.name);
        auto result = challengers.createChallenger(req);
        if (!result) {
            std::cerr << std::format("   [!] createChallenger failed: {}\n", result.error());
            return 1;
        }

        std::cout << "   [✓] Success!\n";
        std::cout << "Server response:\n" << result.value() << '\n';

        if (avatarPath.empty()) return 0;

        auto id = services::ChallengerService::parseChallengerId(result.value());
        if (!id) {
            std::cerr << std::format("   [!] Cannot read the new challenger's id: {}\n", id.error());
            return 1;
        }

        std::cout << "3. Authenticating to upload the avatar...\n";
        auto auth = authenticate(name, secret);
        if (!auth) {
            std::cerr << std::format("   [!] {}\n", auth.error());
            return 1;
        }

        services::AvatarService avatars(http, cfg_);

        std::cout << std::format("4. Uploading avatar '{}'...\n", avatarPath);
        auto uploaded = avatars.uploadAvatar(*id, avatarPath, auth->jwt);
        if (!uploaded) {
            std::cerr << std::format("   [!] Avatar upload failed: {}\n", uploaded.error());
            return 1;
        }

        std::cout << "   [✓] Avatar uploaded.\n";
        return 0;
    }

    int CliApp::executeLogin(const std::vector<std::string>& args) {
        std::string name = args.size() > 0 ? args[0] : "Examinator";
        std::string secret = args.size() > 1 ? args[1] : "s3cret";

        auto http = std::make_shared<http::CprHttpClient>();
        auto serverEncryptor = std::make_shared<crypto::OpensslRsaEncryptor>();
        auto clientKeyManager = std::make_shared<crypto::ClientKeyManager>();
        auto keyService = std::make_shared<services::KeyService>(http, serverEncryptor, cfg_);
        services::AuthService auth(http, serverEncryptor, clientKeyManager, keyService, cfg_);

        std::cout << "1. Generating client RSA key pair...\n";
        if (auto r = clientKeyManager->generate(); !r) {
            std::cerr << std::format("   [!] Failed to generate client keys: {}\n", r.error());
            return 1;
        }
        std::cout << "   [✓] Client key pair generated.\n";

        std::cout << std::format("2. Authenticating as '{}'...\n", name);
        auto loginResult = auth.login(name, secret);
        if (!loginResult) {
            std::cerr << std::format("   [!] Login failed: {}\n", loginResult.error());
            return 1;
        }
        std::cout << "   [✓] Successfully obtained and decrypted JWT token!\n";

        // 3. Test the token
        const auto authResult = loginResult.value();
        std::cout << std::format("   [✓] OTP Seed: {}\n", authResult.otp);
        std::cout << "3. Testing token against "<< cfg_.testUrl <<" endpoint...\n";

        auto r = cpr::Get(
            cpr::Url{cfg_.testUrl},
            cpr::Header{{"Authorization", "Bearer " + authResult.jwt}}
        );

        if (r.error.code == cpr::ErrorCode::OK) {
            std::cout << std::format("   [✓] GET {} response ({}): {}\n", cfg_.testUrl, r.status_code, r.text);
        } else {
            std::cerr << std::format("   [!] GET {} failed: {}\n", cfg_.testUrl, r.error.message);
            return 1;
        }

        // 4. Test OTP
        std::cout << "4. Testing OTP against "<<cfg_.testOtpUrl<<" endpoint...\n";
        auto otpCodeResult = crypto::OtpService::generateTotp(authResult.otp);
        if (!otpCodeResult) {
            std::cerr << std::format("   [!] Failed to generate OTP code: {}\n", otpCodeResult.error());
            return 1;
        }
        std::cout << std::format("   [✓] Generated OTP code: {}\n", *otpCodeResult);

        auto rOtp = cpr::Get(
            cpr::Url{cfg_.testOtpUrl},
            cpr::Parameters{{"ChallengerName", name}, {"OtpCode", *otpCodeResult}}
        );

        if (rOtp.error.code == cpr::ErrorCode::OK) {
            std::cout << std::format("   [✓] GET {} response ({}): {}\n", cfg_.testOtpUrl, rOtp.status_code, rOtp.text);
            if (rOtp.status_code < 200 || rOtp.status_code >= 300) {
                std::cerr << std::format("   [!] GET {} returned error status: {}\n", cfg_.testOtpUrl, rOtp.status_code);
                return 1;
            }
        } else {
            std::cerr << std::format("   [!] GET {} failed: {}\n", cfg_.testOtpUrl, rOtp.error.message);
            return 1;
        }

        // 5. Lobby info
        std::cout << std::format("   [✓] Challenger '{}' is logged in and active in the lobby (status: unready).\n", name);
        std::cout << "   [i] Use 'ready <name> <secret|jwt> [true|false]' to update ready status.\n";

        return 0;
    }

    int CliApp::executeReady(const std::vector<std::string>& args) {
        if (args.size() < 2) {
            std::cerr << "Error: ready requires <challenger_name> <secret|jwt> [true|false]\n";
            std::cerr << "Example: kf_client ready Alice s3cret true\n";
            std::cerr << "         kf_client ready Alice eyJ... true\n";
            return 1;
        }

        std::string name = args[0];
        std::string secretOrToken = args[1];
        bool isReady = true;

        if (args.size() > 2) {
            std::string readyArg = args[2];
            std::transform(readyArg.begin(), readyArg.end(), readyArg.begin(), [](unsigned char c) {
                return static_cast<char>(std::tolower(c));
            });
            if (readyArg == "false" || readyArg == "0" || readyArg == "no" || readyArg == "unready") {
                isReady = false;
            }
        }

        std::string jwtToken = secretOrToken;
        if (!secretOrToken.starts_with("eyJ")) {
            // Treat as secret, perform login to obtain JWT
            auto http = std::make_shared<http::CprHttpClient>();
            auto serverEncryptor = std::make_shared<crypto::OpensslRsaEncryptor>();
            auto clientKeyManager = std::make_shared<crypto::ClientKeyManager>();
            auto keyService = std::make_shared<services::KeyService>(http, serverEncryptor, cfg_);
            services::AuthService auth(http, serverEncryptor, clientKeyManager, keyService, cfg_);

            if (auto r = clientKeyManager->generate(); !r) {
                std::cerr << "   [!] Failed to generate client key pair for authentication.\n";
                return 1;
            }

            std::cout << std::format("1. Authenticating as '{}' to obtain JWT token...\n", name);
            auto loginResult = auth.login(name, secretOrToken);
            if (!loginResult) {
                std::cerr << std::format("   [!] Authentication failed: {}\n", loginResult.error());
                return 1;
            }
            jwtToken = loginResult->jwt;
            std::cout << "   [✓] Authentication successful.\n";
        }

        std::cout << std::format("2. Setting challenger '{}' ready status to {} (JWT protected)...\n", name, isReady ? "ready" : "unready");

        nlohmann::json payload = {
            {"challengerName", name},
            {"isReady", isReady}
        };

        auto r = cpr::Post(
            cpr::Url{cfg_.lobbyUrl},
            cpr::Header{
                {"Content-Type", "application/json"},
                {"Authorization", "Bearer " + jwtToken}
            },
            cpr::Body{payload.dump()}
        );

        if (r.error.code == cpr::ErrorCode::OK) {
            if (r.status_code >= 200 && r.status_code < 300) {
                std::cout << std::format("   [✓] Success (HTTP {}): {}\n", r.status_code, r.text);

                // A match may have started - the server answers with its id and the opponent.
                try {
                    auto body = nlohmann::json::parse(r.text);
                    if (body.contains("matchId") && !body["matchId"].is_null()) {
                        std::cout << std::format("   [⚔] Match started! id={} opponent={}\n",
                            body["matchId"].get<std::string>(),
                            body.value("opponent", std::string("unknown")));
                    }
                } catch (const nlohmann::json::exception&) {
                    // The response was not JSON - the raw text above already told the story.
                }

                return 0;
            } else {
                std::cerr << std::format("   [!] Request failed with HTTP {}: {}\n", r.status_code, r.text);
                return 1;
            }
        } else {
            std::cerr << std::format("   [!] Failed to connect to lobby endpoint: {}\n", r.error.message);
            return 1;
        }
    }

    int CliApp::executeLobby(const std::vector<std::string>& /*args*/) {
        std::cout << "Fetching lobby status...\n";

        auto r = cpr::Get(
            cpr::Url{cfg_.lobbyUrl}
        );

        if (r.error.code == cpr::ErrorCode::OK) {
            if (r.status_code >= 200 && r.status_code < 300) {
                std::cout << std::format("   [✓] HTTP {}:\n", r.status_code);
                try {
                    auto j = nlohmann::json::parse(r.text);
                    int waitingCount = j.value("waitingCount", 0);
                    std::cout << std::format("   Waiting fighters: {}\n", waitingCount);
                    if (j.contains("players") && j["players"].is_array()) {
                        for (const auto& p : j["players"]) {
                            std::string pname = p.value("name", "unknown");
                            bool ready = p.value("isReady", false);
                            std::string status = p.value("status", ready ? "ready" : "unready");
                            std::cout << std::format("   - {} [{}]\n", pname, status);
                        }
                    } else if (j.contains("queue") && j["queue"].is_array()) {
                        for (const auto& q : j["queue"]) {
                            std::cout << std::format("   - {}\n", q.get<std::string>());
                        }
                    }
                } catch (const std::exception& e) {
                    std::cout << r.text << '\n';
                }
                return 0;
            } else {
                std::cerr << std::format("   [!] Request failed with HTTP {}: {}\n", r.status_code, r.text);
                return 1;
            }
        } else {
            std::cerr << std::format("   [!] Failed to connect to lobby endpoint: {}\n", r.error.message);
            return 1;
        }
    }

    int CliApp::executeSendCmd(const std::vector<std::string>& args) {
        std::string host = cfg_.udpHost;
        int port = cfg_.udpPort;
        uint32_t seq = 1;

        std::vector<std::string> positional;
        for (size_t i = 0; i < args.size(); ++i) {
            if (args[i] == "--host" && i + 1 < args.size()) {
                host = args[++i];
            } else if (args[i] == "--port" && i + 1 < args.size()) {
                port = std::stoi(args[++i]);
            } else if (args[i] == "--seq" && i + 1 < args.size()) {
                seq = static_cast<uint32_t>(std::stoul(args[++i]));
            } else {
                positional.push_back(args[i]);
            }
        }

        if (positional.size() < 3) {
            std::cerr << "Error: send-cmd requires <challenger_name> <secret|otp_seed> <command>\n";
            std::cerr << "Example: kf_client send-cmd Alice s3cret \"hit_main\"\n";
            return 1;
        }

        std::string name = positional[0];
        std::string secretOrSeed = positional[1];

        // Combine remaining arguments into commandText
        std::string commandText;
        for (size_t i = 2; i < positional.size(); ++i) {
            if (!commandText.empty()) {
                commandText += " ";
            }
            commandText += positional[i];
        }

        if (!services::UdpCommandService::isStandardGameCommand(commandText)) {
            std::cout << std::format("   [!] Notice: '{}' is not a recognized standard game command pattern.\n", commandText);
        }

        std::string otpSeedOrCode = secretOrSeed;

        // If secretOrSeed is not directly a 6-digit code or likely Base32 seed, attempt HTTP login first
        bool isSixDigits = secretOrSeed.size() == 6 &&
                           std::all_of(secretOrSeed.begin(), secretOrSeed.end(), ::isdigit);

        if (!isSixDigits) {
            // Check if login is possible with this as secret
            auto http = std::make_shared<http::CprHttpClient>();
            auto serverEncryptor = std::make_shared<crypto::OpensslRsaEncryptor>();
            auto clientKeyManager = std::make_shared<crypto::ClientKeyManager>();
            auto keyService = std::make_shared<services::KeyService>(http, serverEncryptor, cfg_);
            services::AuthService auth(http, serverEncryptor, clientKeyManager, keyService, cfg_);

            if (clientKeyManager->generate()) {
                std::cout << std::format("1. Authenticating as '{}' to obtain OTP seed...\n", name);
                auto loginResult = auth.login(name, secretOrSeed);
                if (loginResult) {
                    otpSeedOrCode = loginResult->otp;
                    std::cout << std::format("   [✓] Authenticated! OTP Seed: {}\n", otpSeedOrCode);
                } else {
                    std::cout << std::format("   [i] Login attempt failed ({}), trying as direct OTP seed/code...\n", loginResult.error());
                }
            }
        }

        auto udpClient = std::make_shared<udp::UdpClient>();
        services::UdpCommandService udpService(udpClient, cfg_);

        std::cout << std::format("2. Sending UDP command '{}' to {}:{}...\n", commandText, host, port);
        auto result = udpService.sendCommand(name, otpSeedOrCode, commandText, seq, host, port);
        if (!result) {
            std::cerr << std::format("   [!] Failed to send UDP packet: {}\n", result.error());
            return 1;
        }

        const auto& packet = *result;
        std::cout << "   [✓] UDP packet sent successfully!\n";
        std::cout << std::format("   [✓] Payload: {}\n", packet.toJson());
        std::cout << std::format("   [✓] Sequence: {}, Timestamp: {}, OTP Code: {}\n",
            packet.sequenceNumber, packet.timestamp, packet.otpCode);

        return 0;
    }

} // namespace cli
