cmake_minimum_required(VERSION 3.14)
project(llama_bridge LANGUAGES C CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_POSITION_INDEPENDENT_CODE ON)

# Path to the pinned llama.cpp checkout (0.3.29 vendored commit f05cf46).
if(NOT DEFINED LLAMA_DIR)
    message(FATAL_ERROR "Set -DLLAMA_DIR=/path/to/llama.cpp")
endif()

# Build llama.cpp + ggml as static and fold them into a single self-contained plugin.
set(BUILD_SHARED_LIBS    OFF CACHE BOOL "" FORCE)
set(LLAMA_BUILD_TESTS    OFF CACHE BOOL "" FORCE)
set(LLAMA_BUILD_EXAMPLES OFF CACHE BOOL "" FORCE)
set(LLAMA_BUILD_TOOLS    OFF CACHE BOOL "" FORCE)
set(LLAMA_BUILD_SERVER   OFF CACHE BOOL "" FORCE)
set(LLAMA_CURL           OFF CACHE BOOL "" FORCE)
set(GGML_NATIVE          OFF CACHE BOOL "" FORCE) # do not detect host CPU when cross-compiling

add_subdirectory(${LLAMA_DIR} ${CMAKE_BINARY_DIR}/llama.cpp EXCLUDE_FROM_ALL)

add_library(llama_bridge SHARED llama_bridge.cpp)

target_include_directories(llama_bridge PRIVATE
    ${LLAMA_DIR}/include
    ${LLAMA_DIR}/ggml/include
)

target_link_libraries(llama_bridge PRIVATE llama ggml)

set_target_properties(llama_bridge PROPERTIES
    C_VISIBILITY_PRESET   hidden
    CXX_VISIBILITY_PRESET hidden
)

# Windows: llama_bridge.dll (DllImport("llama_bridge")).
# Android/Linux: libllama_bridge.so (Unity expects the lib* prefix).
if(WIN32)
    set_target_properties(llama_bridge PROPERTIES PREFIX "")
endif()
