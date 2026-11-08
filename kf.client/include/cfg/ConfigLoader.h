//
// Resolves the client configuration: defaults < configuration file
// < environment variables < command-line keys.
//

#ifndef KF_CLIENT_CONFIGLOADER_H
#define KF_CLIENT_CONFIGLOADER_H

#include "cfg/AppConfig.h"
#include <expected>
#include <filesystem>
#include <map>
#include <string>
#include <vector>

namespace cfg {

    // A resolved key/value set. The key names are the ones the client accepts
    // on the command line (--base-url) and inside a configuration file
    // ("base-url"); in the environment they are upper-cased, '-' becomes '_'
    // and the KF_CLIENT_ prefix is added (KF_CLIENT_BASE_URL).
    using ConfigValues = std::map<std::string, std::string>;

    // Takes the recognised configuration arguments out of args, in place, and
    // returns them, so that the command parser only sees command arguments.
    // Accepted forms: --<key> <value> and --<key>=<value>. The special key
    // "config" carries the path of the configuration file.
    [[nodiscard]] std::expected<ConfigValues, std::string> parseArguments(
        std::vector<std::string>& args);

    // Reads a configuration file body: a flat JSON object of configuration keys.
    [[nodiscard]] std::expected<ConfigValues, std::string> parseText(std::string_view text);

    // Reads a configuration file from disk.
    [[nodiscard]] std::expected<ConfigValues, std::string> parseFile(
        const std::filesystem::path& path);

    // Reads the KF_CLIENT_* environment variables.
    [[nodiscard]] ConfigValues readEnvironment();

    // Applies resolved values on top of the development defaults declared in
    // AppConfig.h. Endpoint URLs keep their default path under the resolved
    // base URL unless they are given explicitly.
    [[nodiscard]] std::expected<cfg::AppConfig, std::string> resolve(const ConfigValues& values);

    // The whole chain: defaults < configuration file < environment < arguments.
    // The file comes from --config, from KF_CLIENT_CONFIG, or from
    // kf_client.config.json in the working directory when it is there.
    // Recognised arguments are removed from args.
    [[nodiscard]] std::expected<cfg::AppConfig, std::string> loadAppConfig(
        std::vector<std::string>& args);

} // namespace cfg

#endif // KF_CLIENT_CONFIGLOADER_H
