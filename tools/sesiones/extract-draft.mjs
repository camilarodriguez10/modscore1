// Quita de RulesPage.tsx la logica pura que ahora vive en src/rules/draft.ts.
import { readFileSync, writeFileSync } from 'node:fs';

const path = 'C:/Users/kmiro/Downloads/mod score/src/web/src/routes/RulesPage.tsx';
let source = readFileSync(path, 'utf8').replace(/\r\n/g, '\n');

const start = source.indexOf('// ----------------------------------------------------------------- estado');
const end = source.indexOf('const round2 = ');
if (start < 0 || end < 0 || end < start) throw new Error('markers not found');

source = source.slice(0, start) + source.slice(end);
source = source.replace("const DRAFT_KEY = 'modscore.rulesDraft';\n\n", '');
source = source.replace(
  "import { bandLabel, countOf, formatNotice } from '../i18n/notices';\n",
  "import { bandLabel, countOf, formatNotice } from '../i18n/notices';\n" +
    "import {\n  countChanges,\n  draftFrom,\n  editorFrom,\n  loadEditor,\n  mapValues,\n  parse,\n  saveEditor,\n  text,\n  type BandEdit,\n  type Editor,\n  type MetricEdit,\n} from '../rules/draft';\n",
);

writeFileSync(path, source);
console.log('ok', source.split('\n').length, 'lines');
