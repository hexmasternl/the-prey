const fs = require('fs');
const raw = fs.readFileSync("The Prey Design System.html", "utf8");
const m = raw.match(/<script type="__bundler\/template">([\s\S]*?)<\/script>/);
const tpl = JSON.parse(m[1]);
const a = parseInt(process.argv[2], 10);
const b = parseInt(process.argv[3], 10);
process.stdout.write(tpl.slice(a, b));
