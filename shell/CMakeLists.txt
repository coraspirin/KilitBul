cmake_minimum_required(VERSION 3.20)
project(KilitBulShell LANGUAGES CXX)

# Statik CRT: hedef makinede ayrıca Visual C++ runtime gerekmesin.
set(CMAKE_MSVC_RUNTIME_LIBRARY "MultiThreaded$<$<CONFIG:Debug>:Debug>")
set(CMAKE_CXX_STANDARD 17)

add_library(KilitBulShell SHARED KilitBulShell.cpp KilitBulShell.def)
target_compile_definitions(KilitBulShell PRIVATE UNICODE _UNICODE WIN32_LEAN_AND_MEAN NOMINMAX)
target_link_libraries(KilitBulShell PRIVATE shlwapi shell32 ole32 uuid)

if(MSVC)
  target_compile_options(KilitBulShell PRIVATE /W4 /permissive-)
else()
  # MinGW (yalnızca derlenebilirlik kontrolü için)
  target_link_options(KilitBulShell PRIVATE -static -static-libgcc -static-libstdc++)
endif()
