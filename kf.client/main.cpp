#include "cfg/AppConfig.h"
#include "cfg/ConfigLoader.h"
#include "cli/CliApp.h"

#include <format>
#include <iostream>
#include <span>
#include <string>
#include <vector>

int main(int argc, char* argv[]) {
    std::vector<std::string> args;
    args.reserve(static_cast<std::size_t>(argc));
    for (int i = 0; i < argc; ++i) {
        args.emplace_back(argv[i]);
    }

    // Configuration keys are resolved and taken out of args here, so what CliApp
    // parses is the command and its own arguments only.
    auto config = cfg::loadAppConfig(args);
    if (!config) {
        std::cerr << std::format("Error: {}\n", config.error());
        return 1;
    }

    std::vector<std::string_view> views;
    views.reserve(args.size());
    for (const auto& arg : args) {
        views.emplace_back(arg);
    }

    cli::CliApp app(std::move(*config));
    return app.run(std::span<const std::string_view>{views});
}
