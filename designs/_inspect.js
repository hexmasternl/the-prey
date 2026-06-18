const fs = require('fs');
const path = "The Prey Design System.html";
const raw = fs.readFileSync(path, "utf8");
const m = raw.match(/<script type="__bundler\/template">([\s\S]*?)<\/script>/);
const tpl = JSON.parse(m[1]);
const kw = process.argv[2] || "obby";
function idxOf(k){const o=[];let i=0;while((i=tpl.indexOf(k,i))!==-1){o.push(i);i+=k.length;}return o;}
for (const k of ["Lobby","lobby","Players","player","Joined","Game Setup","Settings","Configuration","Section "]) {
  console.log(k, idxOf(k).length, idxOf(k).slice(0,12));
}
