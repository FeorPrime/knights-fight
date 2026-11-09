# Post-link step for Windows builds: put the cpr DLL that this project actually
# built next to the executable.
#
# The Windows loader resolves an import by the DLL's base name and takes the
# first copy it finds, which is the one sitting beside the executable. On
# Windows that slot is filled by vcpkg's post-link step, which copies whatever
# cpr.dll its package database happens to know about -- a different build from
# the one this project compiled its headers from and linked its import library
# against. When the two export tables disagree the loader stops before main()
# runs, so the binary dies with STATUS_ENTRYPOINT_NOT_FOUND and no output.
#
# Called by CMakeLists.txt as:
#   cmake -DBUILD_ROOT=<build root> -DCONFIG=<configuration> -P this-file
# CONFIG is empty for a single-configuration tree.
#
# Copyright 0xbro. Distributed under the MIT License.

if(NOT BUILD_ROOT)
  message(WARNING "kf_client: AlignCprRuntime.cmake needs -DBUILD_ROOT=<build root>; the import was left to whatever the package manager put beside the executable.")
  return()
endif()

set(DEPS_DIR "${BUILD_ROOT}/_deps/cpr-build")
if(NOT EXISTS "${DEPS_DIR}")
  # Nothing here was built as a cpr DLL (a static build, or a POSIX one where
  # the same step is harmless); leave the link exactly as it is.
  return()
endif()

if(CONFIG)
  set(LIB_DIR "${DEPS_DIR}/cpr/${CONFIG}")
  set(BIN_DIR "${BUILD_ROOT}/${CONFIG}")
else()
  set(LIB_DIR "${DEPS_DIR}")
  set(BIN_DIR "${BUILD_ROOT}")
endif()

if(NOT EXISTS "${BIN_DIR}")
  # This configuration was not the one that just linked.
  return()
endif()

if(EXISTS "${LIB_DIR}/cpr.dll")
  set(CPR_LIBRARY "${LIB_DIR}/cpr.dll")
else()
  file(GLOB_RECURSE CPR_CANDIDATES "${DEPS_DIR}/cpr.dll")
  list(GET CPR_CANDIDATES 0 CPR_LIBRARY)
endif()

if(NOT CPR_LIBRARY)
  return()
endif()

set(IMPORT "${BIN_DIR}/cpr.dll")
if(CPR_LIBRARY STREQUAL IMPORT)
  return()
endif()
file(COPY "${CPR_LIBRARY}" DESTINATION "${BIN_DIR}")
message(STATUS "kf_client: import '${IMPORT}' now comes from the cpr built here (${CPR_LIBRARY}).")
