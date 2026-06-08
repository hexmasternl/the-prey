import { chromium } from 'playwright';
const b = await chromium.launch({ channel: 'chrome' });
const p = await b.newPage({ viewport: { width: 1440, height: 900 } });
await p.goto('http://127.0.0.1:1314/en/', { waitUntil: 'networkidle' });
// scroll through to trigger IntersectionObserver reveals + count-up
for (let y = 0; y <= 3; y++) { await p.evaluate(s => window.scrollTo(0, document.body.scrollHeight * s / 3), y); await p.waitForTimeout(500); }
await p.evaluate(() => window.scrollTo(0, 0)); await p.waitForTimeout(300);
await p.screenshot({ path: '.shot-full-desktop.png', fullPage: true });
// roles list page (stagger) + a how-to page (steps)
await p.goto('http://127.0.0.1:1314/en/roles/', { waitUntil: 'networkidle' }); await p.waitForTimeout(600);
await p.screenshot({ path: '.shot-roles.png', fullPage: true });
await p.goto('http://127.0.0.1:1314/en/how-to/create-a-game/', { waitUntil: 'networkidle' }); await p.waitForTimeout(400);
await p.screenshot({ path: '.shot-howto.png', fullPage: true });
await b.close();
console.log('done');
