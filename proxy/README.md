# HOUSTON Proxy (Vercel Serverless Function)

Key-safe LLM proxy for HOUSTON in T-MINUS (NASA Space Apps 2026).
The API key is stored **only** in Vercel environment variables and is never checked into git or distributed in game builds.

## Features

- **Key safety:** API keys reside exclusively in server-side environment variables.
- **Locked to the game:** only origins in `ALLOWED_ORIGINS` (default `https://ayaan-kashif.github.io`) may call it from a browser; other origins get 403. Desktop builds send no `Origin` header and are allowed; set `HOUSTON_CLIENT_TOKEN` (and the same value as `clientToken` in `houston.config.json`) to require a token from them too. The token ships with the game, so it is a speed bump, not a secret.
- **Request limits:** question at most 300 characters, packet at most 8,000 characters.
- **Rate limiting:** in memory, per client IP (20 requests/minute). This is per serverless instance and resets on a cold start; for a hard limit use Vercel KV / Upstash or Vercel's firewall rate limiting.
- **Rules in the system slot:** for Gemini the rules go in `systemInstruction`, the question is sent as data, and safety filters block medium-or-higher harm (the audience is 12 and up).
- **No diagnostics endpoint:** the old `GET ?diag=models` route was removed because it was public.
- **Concise answers:** the prompt asks for at most 70 words.
- **Dual provider support:** Works with OpenAI-compatible endpoints or Google Gemini API.

## Deployment Steps

1. Install the Vercel CLI (if not already installed):
   ```bash
   npm install -g vercel
   ```

2. Navigate to the `proxy` directory:
   ```bash
   cd proxy
   ```

3. Deploy to Vercel:
   ```bash
   vercel
   ```

4. Set environment variables on Vercel:
   - `HOUSTON_API_KEY`: Your API key (OpenAI, Gemini, or OpenRouter)
   - `LLM_PROVIDER`: `openai` (default) or `gemini`
   - `LLM_MODEL`: e.g. `gpt-4o-mini` or `gemini-1.5-flash`
   - `ALLOWED_ORIGINS` (optional): comma-separated web origins allowed to call the proxy. Add `http://localhost:PORT` when testing a local WebGL build.
   - `HOUSTON_CLIENT_TOKEN` (optional): see "Locked to the game" above.
   - `OPENAI_BASE_URL` (optional): e.g. `https://openrouter.ai/api/v1`

   You can set these via the Vercel dashboard or CLI:
   ```bash
   vercel env add HOUSTON_API_KEY
   vercel env add LLM_PROVIDER
   vercel env add LLM_MODEL
   ```

5. Deploy to production:
   ```bash
   vercel --prod
   ```

6. Copy the deployment URL (e.g., `https://your-project.vercel.app/api/houston`) into `Assets/StreamingAssets/houston.config.json` in your local project:
   ```json
   {
     "endpointUrl": "https://your-project.vercel.app/api/houston",
     "timeoutSec": 8,
     "clientToken": "",
     "language": "en",
     "enabled": true
   }
   ```

## Testing with cURL

```bash
curl -X POST https://your-project.vercel.app/api/houston \
  -H "Content-Type: application/json" \
  -d '{
    "packet": {
      "mission": {"id": "floodwatch_1", "objective": "Image the Indus floodplain"},
      "currentStep": "Bolt 7 next (star pattern)",
      "advisorRecommendation": "Bolt 7 next on j_s1_interstage",
      "faults": [],
      "codex": [{"id": "star_pattern", "shortText": "Tighten opposite bolts to clamp flanges evenly."}]
    },
    "question": "Why a star pattern?"
  }'
```

Expected response:
```json
{
  "text": "Tightening bolts in a star pattern distributes torque evenly across the flange, preventing joint warping and maintaining structural integrity through max-Q. Did you check pass 2?"
}
```

## Gemini setup (T-MINUS team)

1. Vercel project > Settings > General > **Root Directory = `proxy`** (the function is `proxy/api/houston.ts`).
2. Settings > Environment Variables: **`gemini_api_key`** = your Gemini key (Production + Preview). Nothing else is needed: a Gemini key alone selects Gemini, and the model defaults to `gemini-flash-latest` (override with `LLM_MODEL`).
3. Production domain for this team: `nasa-project-proxy.vercel.app`. Use the **production domain** (Project > Domains, e.g. `something.vercel.app`). Preview and `-git-main-` URLs sit behind Vercel Deployment Protection and return a sign-in redirect to the game.
4. Test: `curl -X POST https://<domain>/api/houston -H "Content-Type: application/json" -d '{"question":"What does the EPS do?"}'`
