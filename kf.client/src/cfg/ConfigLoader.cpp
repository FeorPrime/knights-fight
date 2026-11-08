#include "cfg/ConfigLoader.h"
#include "cfg/AppConfig.h"

#include <algorithm>
#include <cctype>
#include <charconv>
#include <cstdlib>
#include <fstream>
#include <iterator>
#include <string>
#include <string_view>
#include <system_error>

#include <format>

#include <nlohmann/json.hpp>

namespace cfg {

    namespace {

        constexpr std::string_view CONFIG_FILE_KEY {"config"};
        constexpr std::string_view ENVIRONMENT_PREFIX {"KF_CLIENT_"};
        constexpr std::string_view DEFAULT_CONFIG_FILE {"kf_client.config.json"};

        // The keys the command line, the configuration file and the environment
        // accept. urlPath is the default path suffix under the base URL for
        // endpoint keys, empty for the keys that are not endpoints.
        constexpr struct ConfigKey {
            std::string_view name;
            std::string_view urlPath;
        } CONFIG_KEYS[] = {
            {"base-url", {}},
            {"public-key-url", "/auth/public-key"},
            {"create-challenger-url", "/challenger"},
            {"login-url", "/auth/login"},
            {"test-url", "/test"},
            {"test-otp-url", "/test-otp"},
            {"lobby-url", "/lobby"},
            {"lobby-status-url", "/lobby/status"},
            {"challenger-avatar-url", "/challengers/{id}/avatar"},
            {CONFIG_FILE_KEY, {}},
            {"udp-host", {}},
            {"udp-port", {}},
        };

        const ConfigKey* findConfigKey(std::string_view name) {
            const auto found = std::ranges::find_if(CONFIG_KEYS, [&](const ConfigKey& key) {
                return key.name == name;
            });
            if (found == std::ranges::cend(CONFIG_KEYS)) return nullptr;
            return &*found;
        }

        std::string keyNames() {
            std::string names;
            for (const auto& key : CONFIG_KEYS) {
                if (key.name == CONFIG_FILE_KEY) continue;
                if (!names.empty()) names += ", ";
                names += key.name;
            }
            return names;
        }

        std::string trimmed(std::string_view value) {
            std::size_t first = 0;
            while (first < value.size() &&
                   std::isspace(static_cast<unsigned char>(value[first]))) {
                ++first;
            }
            std::size_t last = value.size();
            while (last > first &&
                   std::isspace(static_cast<unsigned char>(value[last - 1]))) {
                --last;
            }
            return std::string(value.substr(first, last - first));
        }

        std::string environmentName(std::string_view key) {
            std::string name;
            name.reserve(key.size() + ENVIRONMENT_PREFIX.size());
            name += ENVIRONMENT_PREFIX;
            for (const auto character : key) {
                name += character == '-'
                    ? '_'
                    : static_cast<char>(std::toupper(static_cast<unsigned char>(character)));
            }
            return name;
        }

        std::string environmentValue(std::string_view key) {
            const auto name = environmentName(key);
            // std::getenv was removed by C++20; the C entry point is the one that
            // exists everywhere.
            const auto raw = ::getenv(name.c_str());
            if (raw == nullptr) return {};
            return std::string{raw};
        }

        std::expected<int, std::string> parsePort(std::string_view value) {
            int port = 0;
            const auto [ptr, error] = std::from_chars(value.data(), value.data() + value.size(), port);
            if (error != std::errc{}) {
                return std::unexpected(std::format("The 'udp-port' value must be a number, got '{}'.", value));
            }
            if (ptr != value.data() + value.size()) {
                return std::unexpected(std::format("The 'udp-port' value must be a number, got '{}'.", value));
            }
            if (port < 1 || port > 65535) {
                return std::unexpected("The 'udp-port' value must be a port number between 1 and 65535.");
            }
            return port;
        }

        void assignUrl(cfg::AppConfig& app, std::string_view key, const std::string& value) {
            if (key == "public-key-url") app.publicKeyUrl = value;
            else if (key == "create-challenger-url") app.createChallengerUrl = value;
            else if (key == "login-url") app.loginUrl = value;
            else if (key == "test-url") app.testUrl = value;
            else if (key == "test-otp-url") app.testOtpUrl = value;
            else if (key == "lobby-url") app.lobbyUrl = value;
            else if (key == "lobby-status-url") app.lobbyStatusUrl = value;
            else if (key == "challenger-avatar-url") app.challengerAvatarUrl = value;
        }

    } // namespace

    std::expected<ConfigValues, std::string> parseArguments(std::vector<std::string>& args) {
        ConfigValues values;
        std::vector<std::size_t> consumed;

        // argv[0] is the program name, never a configuration key.
        for (std::size_t index = 1; index < args.size(); ++index) {
            const std::string& argument = args[index];
            if (!argument.starts_with("--")) continue;

            std::string key{argument.substr(2)};
            std::string value;
            bool inlineValue = false;
            if (const auto equals = key.find('='); equals != std::string::npos) {
                value = key.substr(equals + 1);
                key.resize(equals);
                inlineValue = true;
            }

            // Flags the commands own (--host, --port, --seq, --help) stay where they are.
            if (!findConfigKey(key)) continue;

            if (!inlineValue) {
                if (index + 1 >= args.size()) {
                    return std::unexpected(std::format("Missing value for '{}'.", argument));
                }
                value = args[index + 1];
            }
            values[std::move(key)] = std::move(value);
            consumed.push_back(index);
            if (!inlineValue) consumed.push_back(index + 1);
        }

        // Indices ascend as the flags are discovered, so walking them backwards
        // keeps the earlier positions valid while erasing.
        for (auto position = consumed.rbegin(); position != consumed.rend(); ++position) {
            args.erase(args.begin() + static_cast<std::ptrdiff_t>(*position));
        }
        return values;
    }

    std::expected<ConfigValues, std::string> parseText(std::string_view text) {
        auto parsed = nlohmann::json::parse(std::string{text}, nullptr, false);
        if (parsed.is_discarded()) {
            return std::unexpected("The configuration text is not valid JSON.");
        }
        if (!parsed.is_object()) {
            return std::unexpected("The configuration text must be a JSON object.");
        }

        ConfigValues values;
        for (const auto& [key, value] : parsed.items()) {
            if (value.is_string()) {
                values[std::string(key)] = trimmed(value.get<std::string>());
            } else if (value.is_number_integer()) {
                values[std::string(key)] = value.dump();
            } else {
                return std::unexpected(
                    std::format("The '{}' entry must be a string or an integer.", std::string(key)));
            }
        }
        return values;
    }

    std::expected<ConfigValues, std::string> parseFile(const std::filesystem::path& path) {
        std::ifstream file(path);
        if (!file) {
            return std::unexpected(std::format("Cannot open the configuration file '{}'.", path.string()));
        }
        const std::string text{std::istreambuf_iterator<char>(file),
                               std::istreambuf_iterator<char>()};
        auto values = parseText(text);
        if (!values) {
            return std::unexpected(
                std::format("Cannot read '{}': {}", path.string(), values.error()));
        }
        return values;
    }

    ConfigValues readEnvironment() {
        ConfigValues values;
        for (const auto& key : CONFIG_KEYS) {
            const auto raw = environmentValue(key.name);
            if (raw.empty()) continue;
            values[std::string(key.name)] = trimmed(raw);
        }
        return values;
    }

    std::expected<cfg::AppConfig, std::string> resolve(const ConfigValues& values) {
        for (const auto& [key, value] : values) {
            if (!findConfigKey(key)) {
                return std::unexpected(
                    std::format("Unknown configuration key: '{}'. Valid keys: {}", key, keyNames()));
            }
            if (value.empty()) {
                return std::unexpected(std::format("The '{}' value cannot be empty.", key));
            }
        }

        cfg::AppConfig app;

        if (const auto base = values.find("base-url"); base != values.end()) {
            app.baseUrl = base->second;
        }

        for (const auto& key : CONFIG_KEYS) {
            if (key.urlPath.empty()) continue;
            const auto given = values.find(std::string(key.name));
            assignUrl(app, key.name,
                given == values.end()
                    ? app.baseUrl + std::string(key.urlPath)
                    : given->second);
        }

        if (const auto host = values.find("udp-host"); host != values.end()) {
            app.udpHost = host->second;
        }
        if (const auto port = values.find("udp-port"); port != values.end()) {
            const auto parsed = parsePort(port->second);
            if (!parsed) return std::unexpected(parsed.error());
            app.udpPort = *parsed;
        }
        return app;
    }

    std::expected<cfg::AppConfig, std::string> loadAppConfig(std::vector<std::string>& args) {
        auto argumentValues = parseArguments(args);
        if (!argumentValues) return std::unexpected(argumentValues.error());
        ConfigValues arguments = std::move(*argumentValues);
        ConfigValues environment = readEnvironment();

        std::filesystem::path configFile;
        if (const auto given = arguments.find(std::string(CONFIG_FILE_KEY));
            given != arguments.end()) {
            configFile = given->second;
            arguments.erase(given);
        } else if (const auto fromEnvironment = environment.find(std::string(CONFIG_FILE_KEY));
            fromEnvironment != environment.end()) {
            configFile = fromEnvironment->second;
            environment.erase(fromEnvironment);
        } else {
            const std::filesystem::path nextToTheClient{std::string(DEFAULT_CONFIG_FILE)};
            std::error_code ignored;
            if (std::filesystem::exists(nextToTheClient, ignored)) configFile = nextToTheClient;
        }

        ConfigValues fileValues;
        if (!configFile.empty()) {
            auto parsed = parseFile(configFile);
            if (!parsed) return std::unexpected(parsed.error());
            fileValues = std::move(*parsed);
        }

        // map::merge only inserts keys that are still missing, so the layer put
        // in first wins a key the others also carry: arguments beat the
        // environment, which beats the configuration file.
        ConfigValues values = std::move(arguments);
        values.merge(environment);
        values.merge(fileValues);
        return resolve(values);
    }

} // namespace cfg
