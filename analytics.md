Based on AnalyticsService.cs, here's exactly what gets sent:

---

### `app_started`
Fires once on every launch.
- `app_version` — e.g. `1.0.0`

---

### `action_started`
Fires when a user picks any action (built-in or custom).
- `action_id` — e.g. `fix_grammar`, `rewrite`, or the custom action's GUID
- `action_name` — display name, e.g. `"Fix Grammar"`
- `is_builtin` — `true` / `false` (tells you built-in vs custom)
- `provider` — `"OpenRouter"` or `"Groq"`
- `model` — the model name they have configured
- `app_version`

---

### `action_completed`
Fires when the AI stream finishes successfully.
- `action_id`
- `is_builtin`
- `streaming_chunks` — number of SSE delta packets received (proxy for output tokens)
- `result_chars` — total character length of the AI response
- `duration_ms` — time from request start to stream end in milliseconds
- `provider`
- `app_version`

---

### `action_error`
Fires when an action throws an unhandled exception.
- `action_id`
- `is_builtin`
- `error_type` — exception class name only, e.g. `HttpRequestException`, `TaskCanceledException`
- `provider`
- `app_version`

---

### What you can answer from this data
| Question | Answer |
|---|---|
| Most-used actions | Count `action_started` by `action_id` |
| Built-in vs custom usage | Filter `is_builtin` |
| Which AI provider is popular | Count by `provider` |
| Average response speed | Average `duration_ms` in `action_completed` |
| Output volume | Sum/avg `result_chars` or `streaming_chunks` |
| Error rate | `action_error` / `action_started` ratio |
| Which actions error most | Group `action_error` by `action_id` |
| Daily/weekly active installs | Unique `distinct_id` count on any event |