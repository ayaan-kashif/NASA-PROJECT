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
const systemPrompt = `You are HOUSTON, a calm, encouraging flight-engineering mentor inside the T-MINUS education game. Explain the advisor's recommendation and the faults using ONLY the facts in the packet. Never contradict advisorRecommendation, and never invent part names, numbers, ports or rules. If the packet doesn't contain the answer, say so and name the codex entry to read. Audience: students aged 12 and up. At most 70 words, plain language, one idea per sentence, metric units. End with one short question that checks understanding. If language is 'ur', answer in simple Urdu script and keep technical terms in English.`;

export default async function handler(req: VercelRequest, res: VercelResponse) {
  // Set CORS headers for WebGL support
  res.setHeader('Access-Control-Allow-Origin', '*');
  res.setHeader('Access-Control-Allow-Methods', 'POST,OPTIONS');
  res.setHeader(
    'Access-Control-Allow-Headers',
    'X-CSRF-Token, X-Requested-With, Accept, Accept-Version, Content-Length, Content-MD5, Content-Type, Date, X-Api-Version'
  );

  if (req.method === 'OPTIONS') {
    return res.status(200).end();
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
  // 'gemini-flash-latest' is Google's alias for the current Flash model, so retired model names can't break it.
  const model = process.env.LLM_MODEL || (provider === 'gemini' ? 'gemini-flash-latest' : 'gpt-4o-mini');

  const userContent = JSON.stringify({
    packet: packet || {},
    question: question || 'Explain what to do next.'
  });

  try {
    let answer = '';

    if (provider === 'gemini') {
      const url = `https://generativelanguage.googleapis.com/v1beta/models/${model}:generateContent`;
      const payload = {
        contents: [
          { role: 'user', parts: [{ text: `${systemPrompt}\n\nContext and Query:\n${userContent}` }] }
        ],
        generationConfig: {
          maxOutputTokens: 300,
          temperature: 0.4
        }
      };

      const response = await fetch(url, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', 'x-goog-api-key': apiKey }, // header, not URL: keys stay out of logs
        body: JSON.stringify(payload)
      });

      if (!response.ok) {
        console.error('Gemini error', response.status, await response.text());
        return res.status(502).json({ error: 'The AI service is unavailable right now.' });
      }

      const data = await response.json();
      answer = data?.candidates?.[0]?.content?.parts?.[0]?.text || '';
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

      const data = await response.json();
      answer = data?.choices?.[0]?.message?.content || '';
    }

    return res.status(200).json({ text: answer.trim() });
  } catch (err: any) {
    console.error('Proxy error', err);
    return res.status(500).json({ error: 'Internal proxy error' });
  }
}
