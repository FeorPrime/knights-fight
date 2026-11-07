#include "cfg/AppConfig.h"
#include "cli/CliApp.h"

int main(int argc, char* argv[]) {
    cfg::AppConfig cfg;
    cli::CliApp app(std::move(cfg));
    return app.run(argc, argv);
}
