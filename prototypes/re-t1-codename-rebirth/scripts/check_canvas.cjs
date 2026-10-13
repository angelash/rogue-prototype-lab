// Syntax and public-export checks only; this does not run the Canvas host.
const fs = require('fs');
const ts = require('C:/Users/Lenovo/.cursor/extensions/ms-vscode.vscode-typescript-next-5.3.20230808-universal/node_modules/typescript/lib/typescript.js');
const path = 'C:/Users/Lenovo/.cursor/projects/f-workspace-rogue-prototype-lab/canvases/t1-static-analysis.canvas.tsx';
const source = fs.readFileSync(path, 'utf8');
const sdk = fs.readFileSync('C:/Users/Lenovo/.cursor/skills-cursor/canvas/sdk/index.d.ts', 'utf8');
const result = ts.transpileModule(source, { fileName: path, compilerOptions: { jsx: ts.JsxEmit.ReactJSX, target: ts.ScriptTarget.ES2022, module: ts.ModuleKind.ESNext }, reportDiagnostics: true });
const canvasPublicExports = [...sdk.matchAll(/export\s*\{([\s\S]*?)\}\s*from/g)].flatMap(m => m[1].replace(/\/\*[\s\S]*?\*\//g, '').split(',').map(x => x.trim()));
const imports = source.match(/import\s*\{([^}]+)\}/)[1].split(',').map(x => x.trim());
const missing = imports.filter(x => !canvasPublicExports.includes(x));
const syntaxErrors = (result.diagnostics || []).filter(d => d.category === ts.DiagnosticCategory.Error).map(d => ts.flattenDiagnosticMessageText(d.messageText, '\n'));
const out = { date: '2026-10-13', typescript: ts.version, syntaxErrors, missingPublicExports: missing, scope: 'Syntax and SDK export names only; Canvas host compilation/runtime not observed' };
fs.writeFileSync('prototypes/re-t1-codename-rebirth/evidence/canvas-check.json', JSON.stringify(out, null, 2));
console.log(JSON.stringify(out));
if (syntaxErrors.length || missing.length) process.exit(1);
