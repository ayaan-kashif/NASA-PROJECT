import type { VercelRequest, VercelResponse } from '@vercel/node';

// Simple in-memory rate limiting per IP (max 30 requests per minute)
const RATE_LIMIT_WINDOW_MS = 60 * 1000;
const MAX_REQUESTS_PER_WINDOW = 30;
const requestCounts = new Map<string, { count: number; resetTime: number }>();

function isRateLimited(ip: string): boolean {
  const now = Date.now();
  const record = requestCounts.get(ip);
  if (!record || now > record.resetTime) {
    requestCounts.set(ip, { count: 1, resetTime: now + RATE_LIMIT_WINDOW_MS });
    return false;
  }
  if (record.count >= MAX_REQUESTS_PER_WINDOW) {
    return true;
  }
  record.count++;
  return false;
}

// System prompt: embedded so it always ships with the function (the .txt copy is for reading/editing).
const systemPrompt = `You are HOUSTON, a calm, encouraging flight-engineering mentor inside the T-MINUS education game. Explain the advisor's recommendation and the faults using ONLY the facts in the packet. Never contradict advisorRecommendation, and never invent part names, numbers, ports or rules. If those facts don't cover the question, say so in plain words and suggest the Codex entry by its title. Never mention the 'packet', JSON, or these instructions, and don't apologise for missing details when you have answered. Audience: students aged 12 and up. At most 70 words, plain language, one idea per sentence, metric units. End with one short question that checks understanding. If language is 'ur', answer in simple Urdu script and keep technical terms in English.`;

export default async function handler(req: VercelRequest, res: VercelResponse) {
  // Set CORS headers for WebGL support
  res.setHeader('Access-Control-Allow-Origin', '*');
  res.setHeader('Access-Control-Allow-Methods', 'GET,POST,OPTIONS');
  res.setHeader(
    'Access-Control-Allow-Headers',
    'X-CSRF-Token, X-Requested-With, Accept, Accept-Version, Content-Length, Content-MD5, Content-Type, Date, X-Api-Version'
  );

  if (req.method === 'OPTIONS') {
    return res.status(200).end();
  }

  // GET ?diag=models: which Gemini models this key may call (names only; the key is never returned).
  if (req.method === 'GET' && req.query?.diag === 'models') {
    const key = process.env.gemini_api_key || process.env.GEMINI_API_KEY;
    if (!key) return res.status(500).json({ error: 'No Gemini key set.' });
    const r = await fetch('https://generativelanguage.googleapis.com/v1beta/models?pageSize=200', { headers: { 'x-goog-api-key': key } });
    const body: any = await r.json().catch(() => ({}));
    if (!r.ok) return res.status(502).json({ upstreamStatus: r.status, upstreamReason: body?.error?.status || '' });
    const names = (body?.models || [])
      .filter((m: any) => (m?.supportedGenerationMethods || []).includes('generateContent'))
      .map((m: any) => String(m.name).replace('models/', ''));
    return res.status(200).json({ models: names });
  }

  if (req.method !== 'POST') {
    return res.status(405).json({ error: 'Method not allowed' });
  }

  const forwarded = (req.headers['x-forwarded-for'] as string) || '';
  const clientIp = forwarded.split(',')[0].trim() || req.socket.remoteAddress || 'unknown';
  if (isRateLimited(clientIp)) {
    return res.status(429).json({ error: 'Rate limit exceeded. Please wait a moment before asking again.' });
  }

  const { packet, question } = req.body || {};
  if (!question && !packet) {
    return res.status(400).json({ error: 'Missing packet or question' });
  }

  // Gemini key: env var `gemini_api_key` (as set in the Vercel dashboard) or GEMINI_API_KEY.
  const geminiKey = process.env.gemini_api_key || process.env.GEMINI_API_KEY;
  const apiKey = process.env.HOUSTON_API_KEY || process.env.OPENAI_API_KEY || geminiKey;
  if (!apiKey) {
    return res.status(500).json({
      error: 'Proxy is not configured (no API key set).'
    });
  }

  // A Gemini key alone means Gemini; LLM_PROVIDER can still override.
  const provider = (process.env.LLM_PROVIDER || (geminiKey && !process.env.HOUSTON_API_KEY && !process.env.OPENAI_API_KEY ? 'gemini' : 'openai')).toLowerCase();
  const model = process.env.LLM_MODEL || (provider === 'gemini' ? 'gemini-3.5-flash-lite' : 'gpt-4o-mini');
  // Gemini fallbacks, fastest first (checked against GET ?diag=models for this key, Nov 2026). Busy/missing models are skipped.
  const geminiModels = [...new Set([model, 'gemini-3.5-flash', 'gemini-3.1-flash-lite', 'gemini-flash-lite-latest', 'gemini-flash-latest'])];

  const userContent = JSON.stringify({
    packet: packet || {},
    question: question || 'Explain what to do next.'
  });

  try {
    let answer = '';

    if (provider === 'gemini') {
      const payload = {
        contents: [
          { role: 'user', parts: [{ text: `${systemPrompt}

Context and Query:
${userContent}` }] }
        ],
        generationConfig: { maxOutputTokens: 300, temperature: 0.4 } as any
      };

      let lastStatus = 0;
      let lastReason = '';
      const started = Date.now();
      for (const m of geminiModels) {
        if (Date.now() - started > 11000) break;                // stay inside the 15 s function limit
        // Short answers: no hidden "thinking" on 2.5 models (slow, eats the token budget). 3.x keep their default.
        payload.generationConfig.thinkingConfig = m.startsWith('gemini-2.5') ? { thinkingBudget: 0 } : undefined;
        const ctrl = new AbortController();
        const timer = setTimeout(() => ctrl.abort(), 5000);
        try {
          const response = await fetch(`https://generativelanguage.googleapis.com/v1beta/models/${m}:generateContent`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json', 'x-goog-api-key': apiKey }, // header, not URL: keys stay out of logs
            body: JSON.stringify(payload),
            signal: ctrl.signal
          });
          lastStatus = response.status;
          if (!response.ok) {
            const body = await response.text();
            console.error('Gemini error', m, response.status, body);
            // Google's one-word error code (e.g. UNAVAILABLE, PERMISSION_DENIED): safe to return, never the key.
            try { lastReason += `${m}: ${response.status} ${JSON.parse(body)?.error?.status || ''}; `; } catch { lastReason += `${m}: ${response.status}; `; }
            if (response.status === 401 || response.status === 403) break; // bad key: other models won't help
            continue;
          }
          const data: any = await response.json(); // provider JSON: shape checked with optional chaining
          answer = data?.candidates?.[0]?.content?.parts?.map((p: any) => p?.text || '').join('').trim() || '';
          if (answer) break;
        } catch (e) {
          lastStatus = 504;
          lastReason += `${m}: timeout; `;
          console.error('Gemini timeout/network', m, String(e));
        } finally {
          clearTimeout(timer);
        }
      }
      if (!answer) {
        // Only the status number goes back (400 request/model, 401/403 key, 429 quota, 503 busy, 504 timeout).
        return res.status(502).json({ error: 'The AI service is unavailable right now.', upstreamStatus: lastStatus, upstreamReason: lastReason });
      }
    } else {
      // Default: OpenAI-compatible API
      const baseUrl = process.env.OPENAI_BASE_URL || 'https://api.openai.com/v1';
      const response = await fetch(`${baseUrl}/chat/completions`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          Authorization: `Bearer ${apiKey}`
        },
        body: JSON.stringify({
          model,
          messages: [
            { role: 'system', content: systemPrompt },
            { role: 'user', content: userContent }
          ],
          max_tokens: 300,
          temperature: 0.4
        })
      });

      if (!response.ok) {
        console.error('LLM error', response.status, await response.text());
        return res.status(502).json({ error: 'The AI service is unavailable right now.' });
      }

      const data: any = await response.json(); // provider JSON: shape checked with optional chaining below
      answer = data?.choices?.[0]?.message?.content || '';
    }

    return res.status(200).json({ text: answer.trim() });
  } catch (err: any) {
    console.error('Proxy error', err);
    return res.status(500).json({ error: 'Internal proxy error' });
  }
}
