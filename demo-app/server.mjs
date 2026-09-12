import { createServer } from 'node:http';
import { hostname } from 'node:os';

const port = Number(process.env.PORT ?? 8080);
const title = process.env.DEMO_TITLE ?? 'FifthBox demo app';
const message = process.env.DEMO_MESSAGE ?? 'Set DEMO_MESSAGE at deploy time to change this text.';
const color = process.env.DEMO_COLOR ?? '#6750a4';
const instance = hostname();

const escape = (s) =>
  String(s).replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[c]);

const page = (path) => `<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>${escape(title)}</title>
<style>
  :root { color-scheme: light dark; }
  body { margin: 0; min-height: 100vh; display: grid; place-items: center;
         font: 16px/1.5 system-ui, sans-serif; background: ${escape(color)}; color: #fff; }
  main { max-width: 40rem; padding: 2rem; text-align: center; }
  h1 { font-size: clamp(1.75rem, 5vw, 3rem); margin: 0 0 1rem; }
  p.message { font-size: 1.25rem; margin: 0 0 2rem; }
  dl { display: grid; grid-template-columns: auto auto; gap: .25rem 1rem;
       justify-content: center; font-size: .875rem; opacity: .85; }
  dt { text-align: right; font-weight: 600; }
  dd { margin: 0; text-align: left; font-family: ui-monospace, monospace; }
</style>
</head>
<body>
<main>
  <h1>${escape(title)}</h1>
  <p class="message">${escape(message)}</p>
  <dl>
    <dt>instance</dt><dd>${escape(instance)}</dd>
    <dt>port</dt><dd>${port}</dd>
    <dt>path</dt><dd>${escape(path)}</dd>
    <dt>served</dt><dd>${new Date().toISOString()}</dd>
  </dl>
</main>
</body>
</html>`;

createServer((req, res) => {
  const path = (req.url ?? '/').split('?')[0];

  if (path === '/healthz') {
    res.writeHead(200, { 'content-type': 'text/plain' }).end('ok');
    return;
  }

  // Every other path serves the page, so nginx path-prefix routes hit something no matter how they rewrite.
  res.writeHead(200, { 'content-type': 'text/html; charset=utf-8', 'cache-control': 'no-store' });
  res.end(page(path));
}).listen(port, () => console.log(`demo-app listening on ${port} as ${instance}`));
