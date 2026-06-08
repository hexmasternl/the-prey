const pw = await import('file:///C:/Users/EduardKeilholz/AppData/Roaming/npm/node_modules/playwright/index.js');
const chromium = pw.chromium || (pw.default && pw.default.chromium);
const b = await chromium.launch({ channel: 'chrome' });
const p = await b.newPage({ viewport: { width: 390, height: 844 } });
await p.goto('http://127.0.0.1:1314/en/rules/', { waitUntil: 'networkidle' });
const info = await p.evaluate(() => {
  const de = document.documentElement;
  const vw = de.clientWidth;
  const wide = [];
  document.querySelectorAll('*').forEach(el => {
    const r = el.getBoundingClientRect();
    if (r.right > vw + 1 || r.left < -1) wide.push({ tag: el.tagName, cls: (el.className||'').toString().slice(0,36), left: Math.round(r.left), right: Math.round(r.right), w: Math.round(r.width) });
  });
  return { vw, scrollWidth: de.scrollWidth, wideCount: wide.length, wide: wide.slice(0, 16) };
});
console.log(JSON.stringify(info, null, 2));
await b.close();
