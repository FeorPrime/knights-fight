// Configuration resolution tests: argument keys, configuration file, and the
// development defaults. The environment layer is exercised in the shell because
// a test process cannot set its own environment.

#include "cfg/ConfigLoader.h"
#include "cfg/AppConfig.h"

#include <cassert>
#include <filesystem>
#include <fstream>
#include <iostream>
#include <string>
#include <vector>

void test_argument_keys() {
    std::vector<std::string> args{
        "kf_client", "send-cmd", "Alice", "s3cret", "hit_main",
        "--udp-port", "5051", "--base-url=http://api.test:9090/v2",
        "--host", "10.0.0.1"};

    auto values = cfg::parseArguments(args);
    assert(values.has_value());
    assert((*values).at("udp-port") == "5051");
    assert((*values).at("base-url") == "http://api.test:9090/v2");

    // The flags a command owns stay in the argument list.
    assert((*values).count("host") == 0);
    assert(args.size() == 7);
    assert(args[1] == "send-cmd");
    assert(args[5] == "--host");

    // A flag without a value is reported, not silently dropped.
    std::vector<std::string> truncated{"kf_client", "lobby", "--udp-host"};
    auto missing = cfg::parseArguments(truncated);
    assert(!missing.has_value());

    std::cout << "[✓] Argument key test passed.\n";
}

void test_development_defaults() {
    const auto app = cfg::resolve(cfg::ConfigValues{});
    assert(app.has_value());
    assert(app->baseUrl == "http://localhost:8080/api");
    assert(app->loginUrl == "http://localhost:8080/api/auth/login");
    assert(app->udpHost == "127.0.0.1");
    assert(app->udpPort == 5050);

    std::cout << "[✓] Development defaults test passed.\n";
}

void test_base_url_and_single_endpoint() {
    // A new base URL moves every endpoint, except the ones given literally.
    const cfg::ConfigValues values{
        {"base-url", "https://knightfight.example/api"},
        {"test-url", "https://knightfight.example/health"},
        {"udp-port", "6001"},
    };
    const auto app = cfg::resolve(values);
    assert(app.has_value());
    assert(app->publicKeyUrl == "https://knightfight.example/api/auth/public-key");
    assert(app->lobbyUrl == "https://knightfight.example/api/lobby");
    assert(app->testUrl == "https://knightfight.example/health");
    assert(app->udpPort == 6001);
    assert(app->udpHost == "127.0.0.1");

    std::cout << "[✓] Base URL and endpoint override test passed.\n";
}

void test_rejected_values() {
    const cfg::ConfigValues unknown{{"base-ur", "http://localhost:8080/api"}};
    assert(!cfg::resolve(unknown).has_value());

    const cfg::ConfigValues notANumber{{"udp-port", "5050x"}};
    assert(!cfg::resolve(notANumber).has_value());

    const cfg::ConfigValues outOfRange{{"udp-port", "0"}};
    assert(!cfg::resolve(outOfRange).has_value());

    const cfg::ConfigValues empty{{"udp-host", ""}};
    assert(!cfg::resolve(empty).has_value());

    std::cout << "[✓] Rejected values test passed.\n";
}

void test_configuration_file_text() {
    auto values = cfg::parseText(R"({
        "base-url": " http://deployed.host:4040/api ",
        "udp-port": 5051,
        "lobby-url": "http://deployed.host:4040/lobby"
    })");
    assert(values.has_value());
    // Surrounding spaces are trimmed, a number is read as its text.
    assert((*values).at("base-url") == "http://deployed.host:4040/api");
    assert((*values).at("udp-port") == "5051");

    // The file must be a flat object of strings and integers.
    assert(!cfg::parseText(R"({"udp-port": true})").has_value());
    assert(!cfg::parseText("[1, 2]").has_value());
    assert(!cfg::parseText("{ not json").has_value());

    std::cout << "[✓] Configuration file text test passed.\n";
}

void test_configuration_file_on_disk() {
    const auto directory = std::filesystem::temp_directory_path();
    const std::filesystem::path file = directory / "kf_client_test_config.json";
    {
        std::ofstream out(file);
        out << R"({"base-url": "http://file.host:1/api", "udp-port": 5051})";
    }

    auto values = cfg::parseFile(file);
    assert(values.has_value());
    assert((*values).at("base-url") == "http://file.host:1/api");

    // The whole chain: the file loses to the argument keys.
    std::vector<std::string> args{"kf_client", "lobby", "--config",
                                  file.string(), "--udp-port", "5052"};
    const auto app = cfg::loadAppConfig(args);
    assert(app.has_value());
    assert(app->baseUrl == "http://file.host:1/api");
    assert(app->lobbyUrl == "http://file.host:1/api/lobby");
    assert(app->udpPort == 5052);
    assert(args.size() == 2);
    assert(args[1] == "lobby");

    std::filesystem::remove(file);
    std::cout << "[✓] Configuration file test passed.\n";
}

int main() {
    test_argument_keys();
    test_development_defaults();
    test_base_url_and_single_endpoint();
    test_rejected_values();
    test_configuration_file_text();
    test_configuration_file_on_disk();

    std::cout << "All configuration tests passed!\n";
    return 0;
}
