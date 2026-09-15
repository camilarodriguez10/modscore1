// Valida ci.yml y prepara casos para probar el paso "Sin hallazgos de severidad alta".
import { readFileSync, writeFileSync, mkdirSync, rmSync } from 'node:fs';
import { createRequire } from 'node:module';
import { join } from 'node:path';

const root = 'C:/Users/kmiro/Downloads/mod score';
const require = createRequire(join(root, 'src/web/package.json'));
const yaml = require('js-yaml');

const workflow = yaml.load(readFileSync(join(root, '.github/workflows/ci.yml'), 'utf8'));
const codeql = workflow.jobs.codeql;
console.log('YAML valido. Jobs:', Object.keys(workflow.jobs).join(', '));
console.log('codeql.permissions:', JSON.stringify(codeql.permissions));
const analyze = codeql.steps.find((s) => (s.uses ?? '').startsWith('github/codeql-action/analyze'));
console.log('analyze.with:', JSON.stringify(analyze.with));
console.log('pasos codeql:', codeql.steps.map((s) => s.name ?? s.uses.split('@')[0]).join(' | '));
const gate = codeql.steps.find((s) => s.name === 'Sin hallazgos de severidad alta');

const dir = process.argv[2];
rmSync(dir, { recursive: true, force: true });

const sarif = (results, extensionRules) => ({
  version: '2.1.0',
  runs: [{ tool: { driver: { name: 'CodeQL', rules: [] }, extensions: [{ name: 'codeql/csharp-queries', rules: extensionRules }] }, results }],
});

const cases = {
  // esperado: 0 hallazgos, exit 0
  limpio: sarif([{ ruleId: 'cs/log-forging', level: 'warning' }, { ruleId: 'cs/useless-assignment', level: 'note' }], [
    { id: 'cs/log-forging', properties: { 'security-severity': '6.1' } },
    { id: 'cs/useless-assignment', properties: {} },
  ]),
  // esperado: 2 hallazgos (error y warning con 7.5), exit 1
  alto: sarif(
    [
      { ruleId: 'cs/sql-injection', level: 'error' },
      { ruleId: 'cs/weak-crypto', level: 'warning' },
      { ruleId: 'cs/log-forging', level: 'warning' },
      { level: 'note' },
    ],
    [
      { id: 'cs/sql-injection', properties: { 'security-severity': '8.8' } },
      { id: 'cs/weak-crypto', properties: { 'security-severity': '7.5' } },
      { id: 'cs/log-forging', properties: { 'security-severity': '6.1' } },
    ],
  ),
};

// En Windows el interprete se llama python; en el runner, python3.
const script = gate.run.replaceAll('${{ matrix.language }}', 'csharp').replace('python3 -', 'python -');
for (const [name, doc] of Object.entries(cases)) {
  mkdirSync(join(dir, name, 'sarif'), { recursive: true });
  writeFileSync(join(dir, name, 'gate.sh'), script);
  writeFileSync(join(dir, name, 'sarif', 'csharp.sarif'), JSON.stringify(doc));
}
console.log('casos listos en', dir);
