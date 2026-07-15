#include "llama_bridge.h"
#include "llama.h"

#include <chrono>
#include <cstring>
#include <string>
#include <vector>

namespace {

using steady = std::chrono::steady_clock;

inline double ms_since(steady::time_point t0) {
    return std::chrono::duration<double, std::milli>(steady::now() - t0).count();
}

// Build a sampler chain matching the prototype generation params.
// temp <= 0 => greedy (deterministic, used for parity checks).
llama_sampler* build_sampler(float temp, float top_p, float repeat_penalty, unsigned int seed) {
    llama_sampler_chain_params sp = llama_sampler_chain_default_params();
    llama_sampler* chain = llama_sampler_chain_init(sp);

    if (repeat_penalty != 1.0f) {
        // penalty_last_n=64, freq=0, present=0 (repeat penalty only)
        llama_sampler_chain_add(chain, llama_sampler_init_penalties(64, repeat_penalty, 0.0f, 0.0f));
    }
    if (temp <= 0.0f) {
        llama_sampler_chain_add(chain, llama_sampler_init_greedy());
    } else {
        if (top_p < 1.0f) {
            llama_sampler_chain_add(chain, llama_sampler_init_top_p(top_p, 1));
        }
        llama_sampler_chain_add(chain, llama_sampler_init_temp(temp));
        llama_sampler_chain_add(chain, llama_sampler_init_dist(seed));
    }
    return chain;
}

} // namespace

struct lb_context {
    llama_model*       model = nullptr;
    llama_context*     ctx   = nullptr;
    const llama_vocab* vocab = nullptr;
    int                n_ctx = 0;
    llama_token        eot   = -1; // end-of-turn (<end_of_turn>); not always in the EOG set
};

void lb_backend_init(void) { llama_backend_init(); }
void lb_backend_free(void) { llama_backend_free(); }

lb_context* lb_load(const char* model_path, int n_ctx, int n_threads,
                    int n_threads_batch, int n_gpu_layers) {
    if (!model_path) return nullptr;

    llama_model_params mparams = llama_model_default_params();
    mparams.n_gpu_layers = n_gpu_layers; // >0 offloads to GPU (Vulkan build)

    llama_model* model = llama_model_load_from_file(model_path, mparams);
    if (!model) return nullptr;

    llama_context_params cparams = llama_context_default_params();
    cparams.n_ctx           = static_cast<uint32_t>(n_ctx);
    cparams.n_batch         = static_cast<uint32_t>(n_ctx); // single-batch full-prompt prefill
    cparams.n_threads       = n_threads;        // decode (memory-bound)
    cparams.n_threads_batch = n_threads_batch;  // prefill (compute-bound; more cores for long RAG prompts)

    llama_context* ctx = llama_init_from_model(model, cparams);
    if (!ctx) {
        llama_model_free(model);
        return nullptr;
    }

    lb_context* c = new lb_context();
    c->model = model;
    c->ctx   = ctx;
    c->vocab = llama_model_get_vocab(model);
    c->n_ctx = static_cast<int>(llama_n_ctx(ctx));
    c->eot   = llama_vocab_eot(c->vocab);
    return c;
}

void lb_free(lb_context* c) {
    if (!c) return;
    if (c->ctx)   llama_free(c->ctx);
    if (c->model) llama_model_free(c->model);
    delete c;
}

int lb_tokenize(lb_context* c, const char* text, bool add_special, int* out_tokens, int max_tokens) {
    if (!c || !text || !out_tokens) return -1;
    return llama_tokenize(c->vocab, text, static_cast<int32_t>(std::strlen(text)),
                          out_tokens, max_tokens, add_special, /*parse_special*/ true);
}

int lb_format_chat(lb_context* c, const char* system, const char* user, char* out, int out_len) {
    if (!c || !user || !out) return -1;

    // Gemma 4 turn markers are <|turn> (id 105) and <turn|> (id 106, = EOS).
    // These differ from Gemma 1-3's <start_of_turn>/<end_of_turn>, so neither the
    // GGUF's Jinja template (non-Jinja matcher rejects it) nor the built-in "gemma"
    // preset apply. Build the prompt manually with the correct markers; tokenizing
    // with parse_special maps them to the control tokens, and generation stops
    // cleanly on <turn|> (EOG). Matches the prototype's create_chat_completion.
    std::string p;
    if (system && system[0] != '\0') {
        p += "<|turn>system\n"; p += system; p += "<turn|>\n";
    }
    p += "<|turn>user\n";  p += user; p += "<turn|>\n";
    p += "<|turn>model\n";

    int need = static_cast<int>(p.size());
    if (need < out_len) {
        std::memcpy(out, p.c_str(), static_cast<size_t>(need) + 1);
    }
    return need;
}

int lb_generate(lb_context* c, const char* prompt, int max_tokens,
                float temp, float top_p, float repeat_penalty, unsigned int seed,
                bool ignore_eos, lb_token_cb cb, void* user_data, lb_timings* out) {
    if (!c || !prompt) return -1;

    // Reset KV cache so each generation starts from an empty context (single-turn).
    llama_memory_clear(llama_get_memory(c->ctx), true);

    std::vector<llama_token> tokens(c->n_ctx);
    int n_prompt = llama_tokenize(c->vocab, prompt, static_cast<int32_t>(std::strlen(prompt)),
                                  tokens.data(), c->n_ctx, /*add_special*/ true, /*parse_special*/ true);
    if (n_prompt < 0) return -2; // prompt exceeds context
    tokens.resize(n_prompt);

    llama_sampler* smpl = build_sampler(temp, top_p, repeat_penalty, seed);

    lb_timings t{};
    t.n_prompt = n_prompt;

    // prefill
    auto t_prefill0 = steady::now();
    llama_batch batch = llama_batch_get_one(tokens.data(), n_prompt);
    if (llama_decode(c->ctx, batch) != 0) {
        llama_sampler_free(smpl);
        return -3;
    }
    t.prefill_ms = ms_since(t_prefill0);

    // decode loop
    auto t_decode0 = steady::now();
    bool first = true;
    int n_decoded = 0;
    char piece[512];

    for (int i = 0; i < max_tokens; ++i) {
        llama_token id = llama_sampler_sample(smpl, c->ctx, -1); // auto-accepts internally
        if (!ignore_eos && (id == c->eot || llama_vocab_is_eog(c->vocab, id))) break;

        int np = llama_token_to_piece(c->vocab, id, piece, sizeof(piece), /*lstrip*/ 0, /*special*/ false);
        if (first) {
            t.ttft_ms = t.prefill_ms + ms_since(t_decode0);
            first = false;
        }
        ++n_decoded;
        if (np > 0 && cb) {
            std::string s(piece, np);
            if (!cb(s.c_str(), user_data)) break; // caller requested stop
        }

        llama_batch nb = llama_batch_get_one(&id, 1);
        if (llama_decode(c->ctx, nb) != 0) break;
    }

    t.decode_ms = ms_since(t_decode0);
    t.n_decoded = n_decoded;
    if (out) *out = t;

    llama_sampler_free(smpl);
    return 0;
}
