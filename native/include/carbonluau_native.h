#pragma once

#if defined(_WIN32)
#define CARBONLUAU_EXPORT __declspec(dllexport)
#else
#define CARBONLUAU_EXPORT __attribute__((visibility("default")))
#endif

#ifdef __cplusplus
extern "C" {
#endif
CARBONLUAU_EXPORT int carbonluau_probe(void);
#ifdef __cplusplus
}
#endif
