#include "crypto/OtpService.h"
#include "models/UdpPacket.h"
#include "services/UdpCommandService.h"

#include <cassert>
#include <iostream>

void test_packet_json_serialization() {
    models::UdpPacket packet{
        .protocolVersion = 1,
        .sequenceNumber = 42,
        .timestamp = 1731000000000,
        .challengerName = "SirLancelot",
        .otpCode = "123456",
        .commandText = "move up"
    };

    std::string jsonStr = packet.toJson();
    assert(!jsonStr.empty());

    auto parsed = models::UdpPacket::fromJson(jsonStr);
    assert(parsed.has_value());
    assert(parsed->protocolVersion == 1);
    assert(parsed->sequenceNumber == 42);
    assert(parsed->timestamp == 1731000000000);
    assert(parsed->challengerName == "SirLancelot");
    assert(parsed->otpCode == "123456");
    assert(parsed->commandText == "move up");

    std::cout << "[✓] UDP packet JSON serialization test passed.\n";
}

void test_packet_creation_with_otp_seed() {
    // Standard RFC 4226 seed "12345678901234567890" in Base32 = GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ
    std::string seed = "GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ";
    int64_t fixedTime = 59; // T = 59s -> TOTP for SHA1 at T=59s (step 30) = 287082

    auto packetResult = services::UdpCommandService::createPacket(
        "Galahad",
        seed,
        "turn right",
        10,
        fixedTime * 1000
    );

    assert(packetResult.has_value());
    assert(packetResult->protocolVersion == 1);
    assert(packetResult->sequenceNumber == 10);
    assert(packetResult->timestamp == fixedTime * 1000);
    assert(packetResult->challengerName == "Galahad");
    assert(packetResult->commandText == "turn right");
    assert(packetResult->otpCode == "287082");

    std::cout << "[✓] UDP packet creation with OTP seed test passed.\n";
}

void test_packet_creation_with_direct_otp_code() {
    auto packetResult = services::UdpCommandService::createPacket(
        "Arthur",
        "654321",
        "use hand_left",
        1,
        123456789
    );

    assert(packetResult.has_value());
    assert(packetResult->otpCode == "654321");
    assert(packetResult->commandText == "use hand_left");

    std::cout << "[✓] UDP packet creation with direct OTP code test passed.\n";
}

void test_standard_game_commands() {
    assert(services::UdpCommandService::isStandardGameCommand("move up"));
    assert(services::UdpCommandService::isStandardGameCommand("move down"));
    assert(services::UdpCommandService::isStandardGameCommand("move left"));
    assert(services::UdpCommandService::isStandardGameCommand("move right"));
    assert(services::UdpCommandService::isStandardGameCommand("MOVE UP"));
    assert(services::UdpCommandService::isStandardGameCommand("turn left"));
    assert(services::UdpCommandService::isStandardGameCommand("turn right"));
    assert(services::UdpCommandService::isStandardGameCommand("use hand_left"));
    assert(services::UdpCommandService::isStandardGameCommand("use hand_right"));

    assert(!services::UdpCommandService::isStandardGameCommand("jump up"));
    assert(!services::UdpCommandService::isStandardGameCommand("attack"));
    assert(!services::UdpCommandService::isStandardGameCommand(""));

    std::cout << "[✓] Standard game command pattern validation test passed.\n";
}

int main() {
    test_packet_json_serialization();
    test_packet_creation_with_otp_seed();
    test_packet_creation_with_direct_otp_code();
    test_standard_game_commands();

    std::cout << "All UDP tests passed successfully!\n";
    return 0;
}
