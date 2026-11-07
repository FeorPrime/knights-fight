#ifndef KF_CLIENT_CLIAPP_H
#define KF_CLIENT_CLIAPP_H

#include "cfg/AppConfig.h"
#include <expected>
#include <span>
#include <string>
#include <string_view>
#include <vector>

namespace cli {

    // The result of a successful login: the challenger's id and its JWT.
    struct AuthenticatedChallenger {
        long id;
        std::string jwt;
    };

    class CliApp {
    public:
        explicit CliApp(cfg::AppConfig cfg = {});

        // Runs the CLI with given argc/argv arguments, returns exit code
        int run(int argc, char* argv[]);
        int run(std::span<const std::string_view> args);

        void printUsage(std::string_view programName) const;

    private:
        int executeCreate(const std::vector<std::string>& args);
        int executeLogin(const std::vector<std::string>& args);
        int executeReady(const std::vector<std::string>& args);
        int executeLobby(const std::vector<std::string>& args);
        int executeSendCmd(const std::vector<std::string>& args);
        int executeAvatar(const std::vector<std::string>& args);

        // Logs in and returns the challenger's id together with its JWT.
        std::expected<AuthenticatedChallenger, std::string> authenticate(
            const std::string& name, const std::string& secret);

        cfg::AppConfig cfg_;
    };

} // namespace cli

#endif // KF_CLIENT_CLIAPP_H
