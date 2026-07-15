#ifndef LLAMA_BRIDGE_H
#define LLAMA_BRIDGE_H

// Minimal C ABI over llama.cpp for Unity P/Invoke (D-d fallback: direct integration).
// Pinned to the llama.cpp commit vendored by llama-cpp-python 0.3.29 for prototype parity.
#include <stdbool.h>

#if defined(_WIN32)
  #define LB_API __declspec(dllexport)
#else
  #define LB_API __attribute__((visibility("default")))
#endif

#ifdef __cplusplus
extern "C" {
#endif

typedef struct lb_context lb_context;

// Streaming callback. Return false to stop generation early.
typedef bool (*lb_token_cb)(const char* piece, void* user_data);

typedef struct {
    double prefill_ms;   // prompt evaluation wall time
    double decode_ms;    // token generation wall time
    double ttft_ms;      // time to first token (prefill + first decode)
    int    n_prompt;     // prompt token count
    int    n_decoded;    // generated token count
} lb_timings;

LB_API void        lb_backend_init(void);
LB_API void        lb_backend_free(void);

// Load model + create context. n_gpu_layers = 0 for CPU-only; >0 offloads layers to GPU
// (requires a Vulkan-enabled build). n_threads is for decode (memory-bound: few prime
// cores win); n_threads_batch is for prefill (compute-bound: more cores help long RAG
// prompts). Returns NULL on failure.
LB_API lb_context* lb_load(const char* model_path, int n_ctx, int n_threads,
                           int n_threads_batch, int n_gpu_layers);
LB_API void        lb_free(lb_context* c);

// Tokenize for parity checks. Returns token count, or a negative value (-needed) if buffer too small.
LB_API int         lb_tokenize(lb_context* c, const char* text, bool add_special,
                               int* out_tokens, int max_tokens);

// Apply the model's built-in chat template to (optional system + user) turn.
// Returns the formatted length; if it exceeds out_len, call again with a larger buffer.
LB_API int         lb_format_chat(lb_context* c, const char* system, const char* user,
                                  char* out, int out_len);

// Generate from an already-formatted prompt. Streams pieces via cb. Returns 0 on success.
// ignore_eos: keep generating until max_tokens even past EOG (for a pure 256-token
// decode-throughput benchmark). The KV cache is reset at the start of every call,
// so each generation is independent (single-turn Q&A).
LB_API int         lb_generate(lb_context* c, const char* prompt, int max_tokens,
                               float temp, float top_p, float repeat_penalty, unsigned int seed,
                               bool ignore_eos, lb_token_cb cb, void* user_data,
                               lb_timings* out_timings);

#ifdef __cplusplus
}
#endif

#endif // LLAMA_BRIDGE_H
