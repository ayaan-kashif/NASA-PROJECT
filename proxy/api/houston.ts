import type { VercelRequest, VercelResponse } from '@vercel/node';
import * as fs from 'fs';
import * as path from 'path';

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

// Load system prompt
let systemPrompt = "You are HOUSTON, a flight-engineering mentor inside T-MINUS.";
try {
  const promptPath = path.join(__dirname, '..', 'houston_system_prompt.txt');
  if (fs.existsSync(promptPath)) {
    systemPrompt = fs.readFileSync(promptPath, 'utf8').trim();
  }
} catch (e) {
  // Use fallback system prompt
}

export default async function handler(req: VercelRequest, res: VercelResponse) {
  // Set CORS headers for WebGL support
  res.setHeader('Access-Control-Allow-Credentials', 'true');
  res.setHeader('Access-Control-Allow-Origin', '*');
  res.setHeader('Access-Control-Allow-Methods', 'GET,OPTIONS,PATCH,DELETE,POST,PUT');
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

  const clientIp = (req.headers['x-forwarded-for'] as string) || req.socket.remoteAddress || 'unknown';
  if (isRateLimited(clientIp)) {
    return res.status(429).json({ error: 'Rate limit exceeded. Please wait a moment before asking again.' });
  }

  const { packet, question } = req.body || {};
  if (!question && !packet) {
    return res.status(400).json({ error: 'Missing packet or question' });
  }

  const apiKey = process.env.HOUSTON_API_KEY || process.env.OPENAI_API_KEY || process.env.GEMINI_API_KEY;
  if (!apiKey) {
    return res.status(500).json({
      error: 'Proxy server configuration error: No API key set in environment (HOUSTON_API_KEY)'
    });
  }

  const provider = (process.env.LLM_PROVIDER || 'openai').toLowerCase();
  const model = process.env.LLM_MODEL || (provider === 'gemini' ? 'gemini-1.5-flash' : 'gpt-4o-mini');

  const userContent = JSON.stringify({
    packet: packet || {},
    question: question || 'Explain what to do next.'
  });

  try {
    let answer = '';

    if (provider === 'gemini') {
      const url = `https://generativelanguage.googleapis.com/v1beta/models/${model}:generateContent?key=${apiKey}`;
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
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload)
      });

      if (!response.ok) {
        const errText = await response.text();
        return res.status(502).json({ error: `Gemini API error: ${errText}` });
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
        const errText = await response.text();
        return res.status(502).json({ error: `Upstream LLM error: ${errText}` });
      }

      const data = await response.json();
      answer = data?.choices?.[0]?.message?.content || '';
    }

    return res.status(200).json({ text: answer.trim() });
  } catch (err: any) {
    return res.status(500).json({ error: err?.message || 'Internal proxy error' });
  }
}
