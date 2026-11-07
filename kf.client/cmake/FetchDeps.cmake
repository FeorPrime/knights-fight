include(FetchContent)

FetchContent_Declare(
        cpr
        GIT_REPOSITORY https://github.com/libcpr/cpr.git
        GIT_TAG        1.10.5
        GIT_SHALLOW    TRUE      # Only downloads the latest commit (much faster)
        GIT_PROGRESS   TRUE      # Prints download progress to the console
)
set(CPR_USE_SYSTEM_CURL ON)


FetchContent_Declare(
        nlohmann_json
        GIT_REPOSITORY https://github.com/nlohmann/json.git
        GIT_TAG        v3.11.3
        GIT_SHALLOW    TRUE
        GIT_PROGRESS   TRUE
)

FetchContent_MakeAvailable(cpr nlohmann_json)

include(FetchContent)

# Turn off quiet mode so you can see download progress
set(FETCHCONTENT_QUIET OFF)
