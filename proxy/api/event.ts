import type { VercelRequest, VercelResponse } from '@vercel/node';

// Anonymous, opt-in gameplay events from the game (audit F21). Nothing is stored in a database: each valid event is written as one
// JSON line to the function log (Vercel > project > Logs), where the team can count rooms visited and how flights end.
// The game sends only an event name, a random session id and a few short labels; it never sends anything the player typed.

const ALLOWED_EVENTS = new Set(['room_entered', 'flight_ended', 'tutorial_done', 'mission_debriefed']);
const allowedOrigins = new Set(
  (process.env.ALLOWED_ORIGINS || 'https://ayaan-kashif.github.io').split(',').map((o) => o.trim()).filter(Boolean)
);

const WINDOW_MS = 60 * 1000;
const MAX_PER_WINDOW = 60;
const counts = new Map<string, { count: number; reset: number }>();

function limited(ip: string): boolean {
  const now = Date.now();
  const r = counts.get(ip);
  if (!r || now > r.reset) { counts.set(ip, { count: 1, reset: now + WINDOW_MS }); return false; }
  r.count++;
  return r.count > MAX_PER_WINDOW;
}

function short(v: unknown): string {
  return typeof v === 'string' ? v.trim().slice(0, 60) : '';
}

export default async function handler(req: VercelRequest, res: VercelResponse) {
  const origin = (req.headers.origin as string) || '';
  if (origin !== '' && !allowedOrigins.has(origin)) return res.status(403).json({ error: 'Forbidden' });
  if (origin !== '') { res.setHeader('Access-Control-Allow-Origin', origin); res.setHeader('Vary', 'Origin'); }
  res.setHeader('Access-Control-Allow-Methods', 'POST,OPTIONS');
  res.setHeader('Access-Control-Allow-Headers', 'Content-Type');
  if (req.method === 'OPTIONS') return res.status(204).end();
  if (req.method !== 'POST') return res.status(405).json({ error: 'Method not allowed' });

  const forwarded = (req.headers['x-forwarded-for'] as string) || '';
  const ip = forwarded.split(',')[0].trim() || req.socket.remoteAddress || 'unknown';
  if (limited(ip)) return res.status(429).json({ error: 'Too many events' });

  const body = req.body || {};
  const name = short(body.event);
  if (!ALLOWED_EVENTS.has(name)) return res.status(400).json({ error: 'Unknown event' });

  const props: Record<string, string> = {};
  if (body.props && typeof body.props === 'object') {
    for (const [k, v] of Object.entries(body.props as Record<string, unknown>).slice(0, 6)) props[short(k)] = short(v);
  }
  console.log(JSON.stringify({ type: 't-minus-event', event: name, session: short(body.session), version: short(body.version), platform: short(body.platform), props }));
  return res.status(204).end();
}
