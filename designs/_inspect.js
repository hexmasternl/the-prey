const fs = require('fs');
const path = "The Prey Design System.html";
const raw = fs.readFileSync(path, "utf8");
const m = raw.match(/<script type="__bundler\/template">([\s\S]*?)<\/script>/);
const tpl = JSON.parse(m[1]);
const kw = process.argv[2] || "Hunting";
const span = parseInt(process.argv[3] || "600", 10);
let i = 0, n = 0;
while ((i = tpl.indexOf(kw, i)) !== -1) {
  console.log(`\n===== occurrence ${n} at ${i} =====`);
  console.log(tpl.slice(Math.max(0, i - span), i + span));
  i += kw.length; n++;
  if (n >= (parseInt(process.argv[4] || "3", 10))) break;
}
