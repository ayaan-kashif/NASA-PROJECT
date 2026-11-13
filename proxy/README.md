# HOUSTON Proxy (Vercel Serverless Function)

Key-safe LLM proxy for HOUSTON in T-MINUS (NASA Space Apps 2026).
The API key is stored **only** in Vercel environment variables and is never checked into git or distributed in game builds.

## Features

- **Key safety:** API keys reside exclusively in server-side environment variables.
- **CORS enabled:** Supports calls from WebGL builds and local Editor/standalone builds.
- **Rate limiting:** In-memory rate limiting per client IP (30 requests/minute).
- **Concise answers:** Capped at 300 output tokens to ensure concise, prompt engineering guidance.
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
