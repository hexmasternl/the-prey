import { pathToFileURL } from 'url';
const pw = await import(pathToFileURL('C:/Users/EduardKeilholz/AppData/Roaming/npm/node_modules/playwright/index.js').href);
const chromium = pw.chromium ?? pw.default?.chromium;

const html = process.argv[2];
const out = process.argv[3];

const browser = await chromium.launch();
const page = await browser.newPage({
  viewport: { width: 1920, height: 1080 },
  deviceScaleFactor: 1,
});
await page.goto(pathToFileURL(html).href, { waitUntil: 'networkidle' });
// give webfonts a beat to settle
await page.evaluate(() => document.fonts.ready);
await page.waitForTimeout(400);
await page.screenshot({ path: out, clip: { x: 0, y: 0, width: 1920, height: 1080 } });
await browser.close();
console.log('wrote', out);
